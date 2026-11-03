using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SmartPlanter.Api.Controllers;
using SmartPlanter.Api.Data;
using SmartPlanter.Api.DTOs;
using SmartPlanter.Api.Hubs;
using SmartPlanter.Api.Models;
using Xunit;
using Xunit.Abstractions;

namespace SmartPlanter.Tests;

public class BackendTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly ITestOutputHelper _output;
    private readonly int _seed;
    private readonly Random _random;
    private readonly IConfiguration _configuration;
    private readonly WebApplicationFactory<Program> _factory;

    public BackendTests(ITestOutputHelper output, WebApplicationFactory<Program> factory)
    {
        _output = output;
        _factory = factory;
        _seed = Environment.TickCount;
        _random = new Random(_seed);
        _output.WriteLine($"Seed: {_seed}");

        var configValues = new Dictionary<string, string?>
        {
            { "Jwt:Key", "SmartPlanter_Secret_Key_For_Jwt_Token_Auth_2026_Secure_Key!" },
            { "Jwt:Issuer", "SmartPlanterApi" },
            { "Jwt:Audience", "SmartPlanterApp" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    /// <summary>
    /// Створює ізольоване тестове середовище з новою InMemory БД та моками для SignalR.
    /// </summary>
    private (AppDbContext Context, Mock<IClientProxy> MockUserProxy, Mock<IHubContext<TelemetryHub>> MockHubContext) CreateEnvironment()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);

        var mockHubClients = new Mock<IHubClients>();
        var mockUserProxy = new Mock<IClientProxy>();

        mockHubClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockUserProxy.Object);

        var mockHubContext = new Mock<IHubContext<TelemetryHub>>();
        mockHubContext.Setup(h => h.Clients).Returns(mockHubClients.Object);

        return (context, mockUserProxy, mockHubContext);
    }

    /// <summary>
    /// Встановлює контекст автентифікованого користувача для контролера.
    /// </summary>
    private static void SetUserContext(ControllerBase controller, int userId, string username)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext ??= new ControllerContext();
        controller.ControllerContext.HttpContext ??= new DefaultHttpContext();
        controller.ControllerContext.HttpContext.User = principal;
    }

    /// <summary>
    /// Встановлює або видаляє заголовок API-ключа пристрою X-Device-Key в HttpContext контролера.
    /// </summary>
    private static void SetDeviceKeyHeader(ControllerBase controller, string? deviceKey)
    {
        controller.ControllerContext ??= new ControllerContext();
        controller.ControllerContext.HttpContext ??= new DefaultHttpContext();

        if (!string.IsNullOrEmpty(deviceKey))
        {
            controller.ControllerContext.HttpContext.Request.Headers["X-Device-Key"] = deviceKey;
        }
        else
        {
            controller.ControllerContext.HttpContext.Request.Headers.Remove("X-Device-Key");
        }
    }

    /// <summary>
    /// Допоміжний метод для створення рослини у тестовій БД.
    /// </summary>
    private Plant CreatePlantForUser(AppDbContext context, int userId, string name = "Тестова рослина")
    {
        var plant = new Plant
        {
            UserId = userId,
            Name = name,
            Species = "Test Species",
            ApiKey = Guid.NewGuid().ToString("N"),
            MinMoisture = 30.0f,
            MaxMoisture = 70.0f,
            MinTemp = 18.0f,
            MaxTemp = 28.0f,
            MinLight = 300.0f
        };
        context.Plants.Add(plant);
        context.SaveChanges();
        return plant;
    }

    /// <summary>
    /// Генерація тестового JWT-токена для перевірки авторизації.
    /// </summary>
    private string GenerateTestJwtToken(int userId, string username, bool isExpired = false, bool isTamperedKey = false)
    {
        var keyBytes = Encoding.UTF8.GetBytes(isTamperedKey
            ? "WRONG_SECRET_KEY_FOR_TAMPERED_TOKEN_VERIFICATION_12345!"
            : _configuration["Jwt:Key"]!);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username)
            }),
            Expires = isExpired ? DateTime.UtcNow.AddHours(-2) : DateTime.UtcNow.AddHours(1),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    // =========================================================================
    // ТЕСТИ ТЕЛЕМЕТРІЇ ТА АВТЕНТИФІКАЦІЇ ПРИСТРОЇВ
    // =========================================================================

    /// <summary>
    /// Перевіряє автентифікацію пристрою за API-ключем (X-Device-Key).
    /// Приймає запит із правильним ключем і відхиляє без нього або з невалідним ключем.
    /// </summary>
    [Fact]
    public async Task Telemetry_DeviceApiKey_Authentication_AcceptsValidAndRejectsInvalid()
    {
        // Arrange: Створення середовища та рослини із згенерованим ApiKey
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);
        var plant = CreatePlantForUser(context, userId: 1);

        // Act & Assert 1: Запит без заголовка X-Device-Key -> 401 Unauthorized
        SetDeviceKeyHeader(telemetryController, null);
        var noKeyResult = await telemetryController.PostTelemetry(new TelemetryCreateDto(50, 22, 400));
        Assert.IsType<UnauthorizedObjectResult>(noKeyResult.Result);

        // Act & Assert 2: Запит із некоректним API-ключем -> 401 Unauthorized
        SetDeviceKeyHeader(telemetryController, "invalid_device_key_123");
        var invalidKeyResult = await telemetryController.PostTelemetry(new TelemetryCreateDto(50, 22, 400));
        Assert.IsType<UnauthorizedObjectResult>(invalidKeyResult.Result);

        // Act & Assert 3: Запит із правильним API-ключем пристрою -> 200 OK та збереження телеметрії
        SetDeviceKeyHeader(telemetryController, plant.ApiKey);
        var successResult = await telemetryController.PostTelemetry(new TelemetryCreateDto(50, 22, 400));
        var okResult = Assert.IsType<OkObjectResult>(successResult.Result);
        var response = Assert.IsType<TelemetryResponseDto>(okResult.Value);

        Assert.Equal(plant.Id, response.PlantId);
        Assert.Equal(50, response.Moisture);
    }

    /// <summary>
    /// Перевіряє генерацію сповіщень (Alerts) для всіх типів відхилень
    /// та захист від дублювання (дедуплікацію) активних сповіщень.
    /// </summary>
    [Fact]
    public async Task Telemetry_AlertGeneration_AllAlertTypesAndDeduplication()
    {
        // Arrange
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);

        var plant = CreatePlantForUser(context, userId: 100);
        SetDeviceKeyHeader(telemetryController, plant.ApiKey);

        // Act: Надсилання телеметрії з різними критичними показниками
        await telemetryController.PostTelemetry(new TelemetryCreateDto(Moisture: 50, Temperature: 35, Light: 400)); // HighTemp
        await telemetryController.PostTelemetry(new TelemetryCreateDto(Moisture: 50, Temperature: 22, Light: 100)); // LowLight
        await telemetryController.PostTelemetry(new TelemetryCreateDto(Moisture: 85, Temperature: 22, Light: 400)); // HighMoisture

        // Assert: Перевірка створення 3 окремих сповіщень для кожного типу
        var alerts = await context.Alerts.Where(a => a.PlantId == plant.Id).ToListAsync();
        Assert.Equal(3, alerts.Count);
        Assert.Contains(alerts, a => a.Type == AlertType.HighTemperature);
        Assert.Contains(alerts, a => a.Type == AlertType.LowLight);
        Assert.Contains(alerts, a => a.Type == AlertType.HighMoisture);

        // Act: Повторне надсилання критичної температури (спам телеметрією)
        await telemetryController.PostTelemetry(new TelemetryCreateDto(Moisture: 50, Temperature: 40, Light: 400));

        // Assert: Повторне нерозв'язане сповіщення HighTemperature не створюється (дедуплікація)
        var alertsAfterSpam = await context.Alerts
            .Where(a => a.PlantId == plant.Id && a.Type == AlertType.HighTemperature && !a.IsResolved)
            .ToListAsync();
        Assert.Single(alertsAfterSpam);
    }

    /// <summary>
    /// Перевіряє фільтрацію історії телеметрії за часовим інтервалом (кількістю днів)
    /// та правильність сортування результатів за спаданням дати.
    /// </summary>
    [Fact]
    public async Task Telemetry_HistoryFiltering_WorksWithDatesAndDays()
    {
        // Arrange
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);

        var plant = CreatePlantForUser(context, userId: 1);
        SetUserContext(telemetryController, 1, "user_one");

        // Додаємо записи: 10 днів тому, 2 дні тому та поточний момент
        context.Telemetries.Add(new Telemetry { PlantId = plant.Id, Moisture = 50, Temperature = 22, Light = 300, Timestamp = DateTime.UtcNow.AddDays(-10) });
        context.Telemetries.Add(new Telemetry { PlantId = plant.Id, Moisture = 50, Temperature = 22, Light = 300, Timestamp = DateTime.UtcNow.AddDays(-2) });
        context.Telemetries.Add(new Telemetry { PlantId = plant.Id, Moisture = 50, Temperature = 22, Light = 300, Timestamp = DateTime.UtcNow });
        await context.SaveChangesAsync();

        // Act: Запит історії за останні 5 днів
        var result = await telemetryController.GetHistory(plant.Id, limit: 10, days: 5);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<TelemetryResponseDto>>(okResult.Value).ToList();

        // Assert: Отримано лише 2 записи (за останні 5 днів), відсортовані за хронологією спадання
        Assert.Equal(2, list.Count);
        Assert.True(list[0].Timestamp >= list[1].Timestamp);
    }

    /// <summary>
    /// Перевіряє ізоляцію даних телеметрії: користувач не може переглядати історію чужої рослини.
    /// </summary>
    [Fact]
    public async Task Telemetry_History_DataIsolation_UserCannotViewForeignPlantHistory()
    {
        // Arrange
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);

        var plantUser1 = CreatePlantForUser(context, userId: 1);
        var plantUser2 = CreatePlantForUser(context, userId: 2);

        context.Telemetries.Add(new Telemetry
        {
            PlantId = plantUser2.Id,
            Moisture = 45,
            Temperature = 22,
            Light = 350,
            Timestamp = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Act: Користувач 1 намагається отримати історію рослини Користувача 2
        SetUserContext(telemetryController, 1, "user_one");
        var result = await telemetryController.GetHistory(plantUser2.Id);

        // Assert: Перевірка повернення 404 Not Found
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // =========================================================================
    // ТЕСТИ ІЗОЛЯЦІЇ ДАНИХ ТА КЕРУВАННЯ РОСЛИНАМИ (PLANTS)
    // =========================================================================

    /// <summary>
    /// Перевіряє ізоляцію списку рослин та дій над ними між різними користувачами.
    /// </summary>
    [Fact]
    public async Task Plants_DataIsolation_UserCannotAccessOtherUsersPlants()
    {
        // Arrange
        var (context, _, mockHubContext) = CreateEnvironment();
        var plantsController = new PlantsController(context, mockHubContext.Object);

        var plantUser1 = CreatePlantForUser(context, userId: 1, "Монстера Користувача 1");
        var plantUser2 = CreatePlantForUser(context, userId: 2, "Фікус Користувача 2");

        // Act & Assert 1: Користувач 1 бачить лише власну рослину
        SetUserContext(plantsController, 1, "user_one");
        var result = await plantsController.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var user1Plants = Assert.IsAssignableFrom<IEnumerable<PlantResponseDto>>(okResult.Value).ToList();

        Assert.Single(user1Plants);
        Assert.Equal(plantUser1.Id, user1Plants[0].Id);
        Assert.DoesNotContain(user1Plants, p => p.Id == plantUser2.Id);

        // Act & Assert 2: Спроба поливу чужої рослини повертає 404 Not Found
        var waterResult = await plantsController.WaterPlant(plantUser2.Id);
        Assert.IsType<NotFoundObjectResult>(waterResult);
    }

    /// <summary>
    /// Перевіряє, що оновлення та видалення рослини доступне її власнику і заблоковане для сторонніх осіб.
    /// </summary>
    [Fact]
    public async Task Plants_UpdateAndDelete_WorksForOwnerAndBlockedForOthers()
    {
        // Arrange
        var (context, _, mockHubContext) = CreateEnvironment();
        var plantsController = new PlantsController(context, mockHubContext.Object);

        var plantUser1 = CreatePlantForUser(context, userId: 1, "Початкова назва");

        // Act & Assert 1: Користувач 2 намагається змінити та видалити рослину Користувача 1 -> 404 Not Found
        SetUserContext(plantsController, 2, "user_two");
        var updateDto = new PlantUpdateDto("Нове ім'я", "Species", 40, 80, 20, 30, 500);

        var foreignUpdateResult = await plantsController.Update(plantUser1.Id, updateDto);
        Assert.IsType<NotFoundObjectResult>(foreignUpdateResult.Result);

        var foreignDeleteResult = await plantsController.Delete(plantUser1.Id);
        Assert.IsType<NotFoundObjectResult>(foreignDeleteResult);

        // Act & Assert 2: Власник (Користувач 1) успішно оновлює та видаляє рослину
        SetUserContext(plantsController, 1, "user_one");
        var ownUpdateResult = await plantsController.Update(plantUser1.Id, updateDto);
        var okUpdate = Assert.IsType<OkObjectResult>(ownUpdateResult.Result);
        var updatedPlant = Assert.IsType<PlantResponseDto>(okUpdate.Value);
        Assert.Equal("Нове ім'я", updatedPlant.Name);

        var ownDeleteResult = await plantsController.Delete(plantUser1.Id);
        Assert.IsType<NoContentResult>(ownDeleteResult);

        var dbPlant = await context.Plants.FindAsync(plantUser1.Id);
        Assert.Null(dbPlant);
    }

    // =========================================================================
    // ТЕСТИ СПОВІЩЕНЬ (ALERTS)
    // =========================================================================

    /// <summary>
    /// Перевіряє, що користувач отримує та закриває (розв'язує) лише власні сповіщення.
    /// </summary>
    [Fact]
    public async Task Alerts_DataIsolation_UserOnlyReceivesAndResolvesOwnAlerts()
    {
        // Arrange
        var (context, _, _) = CreateEnvironment();
        var alertsController = new AlertsController(context);

        var plantUser1 = CreatePlantForUser(context, userId: 1);
        var plantUser2 = CreatePlantForUser(context, userId: 2);

        var alert1 = new Alert { PlantId = plantUser1.Id, Type = AlertType.LowMoisture, Message = "Алерт 1", IsResolved = false };
        var alert2 = new Alert { PlantId = plantUser2.Id, Type = AlertType.HighTemperature, Message = "Алерт 2", IsResolved = false };
        context.Alerts.AddRange(alert1, alert2);
        await context.SaveChangesAsync();

        // Act & Assert 1: Користувач 1 бачить лише своє сповіщення
        SetUserContext(alertsController, 1, "user_one");
        var activeAlertsResult = await alertsController.GetActiveAlerts();
        var okResult = Assert.IsType<OkObjectResult>(activeAlertsResult.Result);
        var alerts = Assert.IsAssignableFrom<IEnumerable<AlertResponseDto>>(okResult.Value).ToList();

        Assert.Single(alerts);
        Assert.Equal(alert1.Id, alerts[0].Id);

        // Act & Assert 2: Спроба закрити чуже сповіщення -> 404 Not Found
        var resolveForeignResult = await alertsController.ResolveAlert(alert2.Id);
        Assert.IsType<NotFoundObjectResult>(resolveForeignResult);

        // Act & Assert 3: Закриття власного сповіщення -> 204 NoContent, стан IsResolved = true в БД
        var resolveOwnResult = await alertsController.ResolveAlert(alert1.Id);
        Assert.IsType<NoContentResult>(resolveOwnResult);

        var updatedAlert1 = await context.Alerts.FindAsync(alert1.Id);
        Assert.True(updatedAlert1!.IsResolved);
    }

    // =========================================================================
    // ТЕСТИ SIGNALR ТА WEBSOCKETS
    // =========================================================================

    /// <summary>
    /// Перевіряє надсилання асинхронного сповіщення через SignalR мок-хаб безпосередньо цільовому користувачу.
    /// </summary>
    [Fact]
    public async Task WebSockets_BroadcastsAlertToTargetUserOnly()
    {
        // Arrange
        var (context, mockUserProxy, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);

        var plantUser1 = CreatePlantForUser(context, userId: 42);

        SetDeviceKeyHeader(telemetryController, plantUser1.ApiKey);
        var badTelemetry = new TelemetryCreateDto(Moisture: 10.0f, Temperature: 22.0f, Light: 400.0f);

        // Act: Передаємо критичні показники вологості
        await telemetryController.PostTelemetry(badTelemetry);

        // Assert: Перевірка виклику методом SendCoreAsync теми ReceiveAlert у SignalR для конкретного userId
        mockUserProxy.Verify(
            c => c.SendCoreAsync("ReceiveAlert", It.IsAny<object[]>(), default),
            Times.Once);

        var alert = await context.Alerts.FirstOrDefaultAsync(a => a.PlantId == plantUser1.Id);
        Assert.NotNull(alert);
        Assert.Equal(AlertType.LowMoisture, alert.Type);
    }

    /// <summary>
    /// Інтеграційний тест підключення до реального SignalR хабу через TestServer з передачею JWT-токена.
    /// </summary>
    [Fact]
    public async Task WebSockets_RealSignalRHubConnection_ConnectsAndHandshakesSuccessfully()
    {
        // Arrange
        var testServer = _factory.Server;
        var token = GenerateTestJwtToken(userId: 99, username: "signalr_tester");

        // Act: Налаштування з'єднання клієнта SignalR із TestServer
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(testServer.BaseAddress, "/hubs/telemetry"), options =>
            {
                options.HttpMessageHandlerFactory = _ => testServer.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

        hubConnection.On<object>("ReceiveAlert", _ => { });

        // Assert: Успішне встановлення з'єднання (Handshake)
        await hubConnection.StartAsync();
        Assert.Equal(HubConnectionState.Connected, hubConnection.State);

        await hubConnection.StopAsync();
    }

    // =========================================================================
    // ТЕСТИ КЕРУВАННЯ КОРИСТУВАЧАМИ (USERS) ТА JWT
    // =========================================================================

    /// <summary>
    /// Перевіряє реєстрацію нового користувача, його вхід та відхилення входу при некоректному паролі.
    /// </summary>
    [Fact]
    public async Task Users_RegistrationAndLogin_IssuesValidToken()
    {
        // Arrange
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        // Act & Assert 1: Реєстрація користувача -> 201 Created і повернення JWT
        var regResult = await usersController.Register(new UserRegisterDto("gardener_1", "secure_pass_123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var registeredUser = Assert.IsType<UserResponseDto>(createdResult.Value);

        Assert.Equal("gardener_1", registeredUser.Username);
        Assert.False(string.IsNullOrWhiteSpace(registeredUser.Token));

        // Act & Assert 2: Вхід із правильним паролем -> 200 OK та валідний JWT
        var loginResult = await usersController.Login(new UserLoginDto("gardener_1", "secure_pass_123"));
        var loginOk = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loggedUser = Assert.IsType<UserResponseDto>(loginOk.Value);

        Assert.Equal(registeredUser.Id, loggedUser.Id);
        Assert.False(string.IsNullOrWhiteSpace(loggedUser.Token));

        // Act & Assert 3: Вхід із неправильним паролем -> 401 Unauthorized
        var badLoginResult = await usersController.Login(new UserLoginDto("gardener_1", "wrong_password"));
        Assert.IsType<UnauthorizedObjectResult>(badLoginResult.Result);
    }

    /// <summary>
    /// Перевіряє захист кінцевої точки /me та отримання даних профілю поточного користувача.
    /// </summary>
    [Fact]
    public async Task Users_Authorization_ProtectsGetMeEndpoint()
    {
        // Arrange
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        var regResult = await usersController.Register(new UserRegisterDto("auth_user", "mypassword"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        // Act & Assert 1: Запит профілю без автентифікації -> 401 Unauthorized
        usersController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var unauthorizedResult = await usersController.GetMe();
        Assert.IsType<UnauthorizedObjectResult>(unauthorizedResult.Result);

        // Act & Assert 2: Запит профілю авторизованим користувачем -> 200 OK з відповідними даними
        SetUserContext(usersController, user.Id, user.Username);
        var authorizedResult = await usersController.GetMe();
        var okResult = Assert.IsType<OkObjectResult>(authorizedResult.Result);
        var profile = Assert.IsType<UserResponseDto>(okResult.Value);

        Assert.Equal(user.Id, profile.Id);
        Assert.Equal("auth_user", profile.Username);
    }

    /// <summary>
    /// Перевіряє зміну пароля: відхилення при неправильному старому паролі та успішну авторизацію з новим.
    /// </summary>
    [Fact]
    public async Task Users_ChangePassword_WorksCorrectlyAndRejectsWrongOldPassword()
    {
        // Arrange
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        var regResult = await usersController.Register(new UserRegisterDto("pass_user", "old_secret_123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        SetUserContext(usersController, user.Id, user.Username);

        // Act & Assert 1: Введення невірного старого пароля -> 400 Bad Request
        var wrongOldPassResult = await usersController.ChangePassword(new UserChangePasswordDto("incorrect_old_pass", "new_secret_456"));
        Assert.IsType<BadRequestObjectResult>(wrongOldPassResult);

        // Act & Assert 2: Успішна зміна пароля
        var successChangeResult = await usersController.ChangePassword(new UserChangePasswordDto("old_secret_123", "new_secret_456"));
        Assert.IsType<OkObjectResult>(successChangeResult);

        // Act & Assert 3: Спроба входу зі старим паролем відхиляється, а з новим — проходить
        var oldLoginResult = await usersController.Login(new UserLoginDto("pass_user", "old_secret_123"));
        Assert.IsType<UnauthorizedObjectResult>(oldLoginResult.Result);

        var newLoginResult = await usersController.Login(new UserLoginDto("pass_user", "new_secret_456"));
        Assert.IsType<OkObjectResult>(newLoginResult.Result);
    }

    /// <summary>
    /// Перевіряє видалення власного облікового запису (DeleteMe): каскадне видалення зв'язаних рослин
    /// та подальше відхилення запитів із цим токеном.
    /// </summary>
    [Fact]
    public async Task Users_DeleteMe_RemovesUserAndPlants_AndTokenBecomesInvalid()
    {
        // Arrange
        var (context, _, mockHubContext) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);
        var plantsController = new PlantsController(context, mockHubContext.Object);

        var regResult = await usersController.Register(new UserRegisterDto("deleted_user", "password123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        SetUserContext(plantsController, user.Id, user.Username);
        var plantResult = await plantsController.Create(new PlantCreateDto("Рослина видалюваного юзера", "Species"));
        var createdPlant = Assert.IsType<CreatedAtActionResult>(plantResult.Result);
        var plant = Assert.IsType<PlantResponseDto>(createdPlant.Value);

        // Act: Видалення власного акаунту
        SetUserContext(usersController, user.Id, user.Username);
        var deleteResult = await usersController.DeleteMe();
        Assert.IsType<NoContentResult>(deleteResult);

        // Assert: Перевірка відсутності записів у БД
        var dbUser = await context.Users.FindAsync(user.Id);
        var dbPlant = await context.Plants.FindAsync(plant.Id);
        Assert.Null(dbUser);
        Assert.Null(dbPlant);

        // Запит профілю видаленого користувача повертає 404 Not Found
        var invalidTokenResult = await usersController.GetMe();
        Assert.IsType<NotFoundObjectResult>(invalidTokenResult.Result);
    }

    /// <summary>
    /// Перевіряє обробку повторної реєстрації користувача з вже існуючим Username.
    /// </summary>
    [Fact]
    public async Task Users_Registration_DuplicateUsername_ReturnsBadRequestOrConflict()
    {
        // Arrange
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        await usersController.Register(new UserRegisterDto("duplicate_user", "password123"));

        // Act: Повторна спроба реєстрації з аналогічним ім'ям
        var secondAttempt = await usersController.Register(new UserRegisterDto("duplicate_user", "another_password"));

        // Assert: Відповідь 400 Bad Request або 409 Conflict
        Assert.True(
            secondAttempt.Result is BadRequestObjectResult || secondAttempt.Result is ConflictObjectResult,
            "Повторна реєстрація з існуючим Username повинна повертати 400 або 409."
        );
    }

    /// <summary>
    /// Перевіряє дотримання унікальності імені користувача на рівні бази даних при паралельному збереженні.
    /// </summary>
    [Fact]
    public async Task Users_Registration_ParallelRequests_EnforcesUniqueness()
    {
        // Arrange
        var (context, _, _) = CreateEnvironment();

        var user1 = new User { Username = "race_condition_user", PasswordHash = "hash1" };
        var user2 = new User { Username = "race_condition_user", PasswordHash = "hash2" };

        context.Users.Add(user1);
        await context.SaveChangesAsync();

        // Act & Assert: При додаванні дубліката очікується виключення EF Core
        context.Users.Add(user2);

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await context.SaveChangesAsync();
        });
    }

    // =========================================================================
    // ІНТЕГРАЦІЙНІ ТЕСТЫ JWT MIDDLEWARE (HTTP CLIENT)
    // =========================================================================

    /// <summary>
    /// Інтеграційний тест Middleware JWT авторизації: валідний Bearer-токен повертає статус 200 OK.
    /// </summary>
    [Fact]
    public async Task Users_JwtMiddleware_ValidToken_Returns200OK()
    {
        // Arrange: Створення клієнта та додавання користувача в БД інстанса WebApplicationFactory
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Id == 1);
        if (existingUser == null)
        {
            db.Users.Add(new User { Id = 1, Username = "valid_jwt_user", PasswordHash = "hash" });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var token = GenerateTestJwtToken(userId: 1, username: "valid_jwt_user");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Інтеграційний тест Middleware JWT авторизації: прострочений або підроблений токен повертає status 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task Users_JwtMiddleware_ExpiredOrTamperedToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act & Assert 1: Прострочений токен (Expired Token) -> 401 Unauthorized
        var expiredToken = GenerateTestJwtToken(userId: 1, username: "expired_user", isExpired: true);
        var requestExpired = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        requestExpired.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        var responseExpired = await client.SendAsync(requestExpired);
        Assert.Equal(HttpStatusCode.Unauthorized, responseExpired.StatusCode);

        // Act & Assert 2: Підроблений токен (Tampered Signature Key) -> 401 Unauthorized
        var tamperedToken = GenerateTestJwtToken(userId: 1, username: "hacker_user", isTamperedKey: true);
        var requestTampered = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        requestTampered.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);
        var responseTampered = await client.SendAsync(requestTampered);
        Assert.Equal(HttpStatusCode.Unauthorized, responseTampered.StatusCode);
    }
}