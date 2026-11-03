using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SmartPlanter.Api.Controllers;
using SmartPlanter.Api.Data;
using SmartPlanter.Api.DTOs;
using SmartPlanter.Api.Hubs;
using SmartPlanter.Api.Models;
using Xunit;
using Xunit.Abstractions;

namespace SmartPlanter.Tests;

public class BackendTests
{
    private readonly ITestOutputHelper _output;
    private readonly int _seed;
    private readonly Random _random;
    private readonly IConfiguration _configuration;

    public BackendTests(ITestOutputHelper output)
    {
        _output = output;
        _seed = Environment.TickCount;
        _random = new Random(_seed);
        _output.WriteLine($"Seed: {_seed}");

        // Створюємо конфігурацію в пам'яті для генерації JWT-токенів.
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
    /// Створює ізольоване тестове середовище з InMemory-базою даних і моками SignalR.
    /// </summary>
    private (AppDbContext Context, Mock<IClientProxy> MockUserProxy,
        Mock<IHubContext<TelemetryHub>> MockHubContext) CreateEnvironment()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);

        var mockHubClients = new Mock<IHubClients>();
        var mockUserProxy = new Mock<IClientProxy>();

        mockHubClients
            .Setup(clients => clients.User(It.IsAny<string>()))
            .Returns(mockUserProxy.Object);

        var mockHubContext = new Mock<IHubContext<TelemetryHub>>();
        mockHubContext
            .Setup(hub => hub.Clients)
            .Returns(mockHubClients.Object);

        return (context, mockUserProxy, mockHubContext);
    }

    /// <summary>
    /// Встановлює в контролері контекст автентифікованого користувача.
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
    /// Встановлює або видаляє заголовок X-Device-Key у тестовому HTTP-контексті.
    /// </summary>
    private static void SetDeviceKeyHeader(ControllerBase controller, string? deviceKey)
    {
        controller.ControllerContext ??= new ControllerContext();
        controller.ControllerContext.HttpContext ??= new DefaultHttpContext();

        if (!string.IsNullOrWhiteSpace(deviceKey))
        {
            controller.ControllerContext.HttpContext.Request.Headers["X-Device-Key"] = deviceKey;
        }
        else
        {
            controller.ControllerContext.HttpContext.Request.Headers.Remove("X-Device-Key");
        }
    }

    /// <summary>
    /// Створює тестову рослину та прив'язує її до власника.
    /// </summary>
    private static Plant CreatePlantForUser(
        AppDbContext context,
        int userId,
        string name = "Тестова рослина")
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

    // =========================================================================
    // ТЕСТИ ТЕЛЕМЕТРІЇ ТА АВТЕНТИФІКАЦІЇ ПРИСТРОЇВ
    // =========================================================================

    /// <summary>
    /// Перевіряє приймання правильного API-ключа пристрою та відхилення відсутнього або неправильного ключа.
    /// </summary>
    [Fact]
    public async Task Telemetry_DeviceApiKey_Authentication_AcceptsValidAndRejectsInvalid()
    {
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);
        var plant = CreatePlantForUser(context, userId: 1);

        // Запит без API-ключа має бути відхилений.
        SetDeviceKeyHeader(telemetryController, null);
        var noKeyResult = await telemetryController.PostTelemetry(
            new TelemetryCreateDto(50, 22, 400));
        Assert.IsType<UnauthorizedObjectResult>(noKeyResult.Result);

        // Запит із неправильним API-ключем має бути відхилений.
        SetDeviceKeyHeader(telemetryController, "invalid_device_key_123");
        var invalidKeyResult = await telemetryController.PostTelemetry(
            new TelemetryCreateDto(50, 22, 400));
        Assert.IsType<UnauthorizedObjectResult>(invalidKeyResult.Result);

        // Запит із правильним API-ключем має бути успішним.
        SetDeviceKeyHeader(telemetryController, plant.ApiKey);
        var successResult = await telemetryController.PostTelemetry(
            new TelemetryCreateDto(50, 22, 400));

        var okResult = Assert.IsType<OkObjectResult>(successResult.Result);
        var response = Assert.IsType<TelemetryResponseDto>(okResult.Value);

        Assert.Equal(plant.Id, response.PlantId);
        Assert.Equal(50, response.Moisture);
    }

    /// <summary>
    /// Перевіряє збереження телеметрії після успішної автентифікації пристрою.
    /// </summary>
    [Fact]
    public async Task Telemetry_ValidDeviceKey_SavesTelemetryRecord()
    {
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);
        var plant = CreatePlantForUser(context, userId: 7);
        SetDeviceKeyHeader(telemetryController, plant.ApiKey);

        var result = await telemetryController.PostTelemetry(
            new TelemetryCreateDto(55, 23, 450));

        Assert.IsType<OkObjectResult>(result.Result);

        var savedTelemetry = await context.Telemetries
            .FirstOrDefaultAsync(item => item.PlantId == plant.Id);

        Assert.NotNull(savedTelemetry);
        Assert.Equal(55, savedTelemetry.Moisture);
        Assert.Equal(23, savedTelemetry.Temperature);
        Assert.Equal(450, savedTelemetry.Light);
    }

    /// <summary>
    /// Перевіряє створення сповіщень для різних критичних показників і відсутність дублікатів активних сповіщень.
    /// </summary>
    [Fact]
    public async Task Telemetry_AlertGeneration_AllAlertTypesAndDeduplication()
    {
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);
        var plant = CreatePlantForUser(context, userId: 100);
        SetDeviceKeyHeader(telemetryController, plant.ApiKey);

        // Створюємо сповіщення для високої температури, низького освітлення та високої вологості.
        await telemetryController.PostTelemetry(
            new TelemetryCreateDto(Moisture: 50, Temperature: 35, Light: 400));
        await telemetryController.PostTelemetry(
            new TelemetryCreateDto(Moisture: 50, Temperature: 22, Light: 100));
        await telemetryController.PostTelemetry(
            new TelemetryCreateDto(Moisture: 85, Temperature: 22, Light: 400));

        var alerts = await context.Alerts
            .Where(alert => alert.PlantId == plant.Id)
            .ToListAsync();

        Assert.Equal(3, alerts.Count);
        Assert.Contains(alerts, alert => alert.Type == AlertType.HighTemperature);
        Assert.Contains(alerts, alert => alert.Type == AlertType.LowLight);
        Assert.Contains(alerts, alert => alert.Type == AlertType.HighMoisture);

        // Повторне критичне значення не повинно створювати друге активне сповіщення того самого типу.
        await telemetryController.PostTelemetry(
            new TelemetryCreateDto(Moisture: 50, Temperature: 40, Light: 400));

        var activeTemperatureAlerts = await context.Alerts
            .Where(alert => alert.PlantId == plant.Id
                && alert.Type == AlertType.HighTemperature
                && !alert.IsResolved)
            .ToListAsync();

        Assert.Single(activeTemperatureAlerts);
    }

    /// <summary>
    /// Перевіряє, що звичайні показники телеметрії не спричиняють надсилання сповіщення через SignalR.
    /// </summary>
    [Fact]
    public async Task Telemetry_NormalValues_DoNotBroadcastAlert()
    {
        var (context, mockUserProxy, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);
        var plant = CreatePlantForUser(context, userId: 42);
        SetDeviceKeyHeader(telemetryController, plant.ApiKey);

        await telemetryController.PostTelemetry(
            new TelemetryCreateDto(Moisture: 50, Temperature: 22, Light: 400));

        mockUserProxy.Verify(
            client => client.SendCoreAsync(
                "ReceiveAlert",
                It.IsAny<object[]>(),
                default),
            Times.Never);

        Assert.Empty(await context.Alerts
            .Where(alert => alert.PlantId == plant.Id)
            .ToListAsync());
    }

    /// <summary>
    /// Перевіряє фільтрацію історії телеметрії за кількістю днів і сортування від нових записів до старих.
    /// </summary>
    [Fact]
    public async Task Telemetry_HistoryFiltering_WorksWithDatesAndDays()
    {
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);
        var plant = CreatePlantForUser(context, userId: 1);
        SetUserContext(telemetryController, 1, "user_one");

        context.Telemetries.Add(new Telemetry
        {
            PlantId = plant.Id,
            Moisture = 50,
            Temperature = 22,
            Light = 300,
            Timestamp = DateTime.UtcNow.AddDays(-10)
        });
        context.Telemetries.Add(new Telemetry
        {
            PlantId = plant.Id,
            Moisture = 50,
            Temperature = 22,
            Light = 300,
            Timestamp = DateTime.UtcNow.AddDays(-2)
        });
        context.Telemetries.Add(new Telemetry
        {
            PlantId = plant.Id,
            Moisture = 50,
            Temperature = 22,
            Light = 300,
            Timestamp = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await telemetryController.GetHistory(plant.Id, limit: 10, days: 5);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var records = Assert.IsAssignableFrom<IEnumerable<TelemetryResponseDto>>(okResult.Value)
            .ToList();

        Assert.Equal(2, records.Count);
        Assert.True(records[0].Timestamp >= records[1].Timestamp);
    }

    /// <summary>
    /// Перевіряє заборону перегляду історії телеметрії рослини іншого користувача.
    /// </summary>
    [Fact]
    public async Task Telemetry_History_DataIsolation_UserCannotViewForeignPlantHistory()
    {
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

        SetUserContext(telemetryController, 1, "user_one");
        var result = await telemetryController.GetHistory(plantUser2.Id);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // =========================================================================
    // ТЕСТИ ІЗОЛЯЦІЇ ДАНИХ ТА КЕРУВАННЯ РОСЛИНАМИ
    // =========================================================================

    /// <summary>
    /// Перевіряє, що користувач бачить лише власні рослини та не може поливати чужі.
    /// </summary>
    [Fact]
    public async Task Plants_DataIsolation_UserCannotAccessOtherUsersPlants()
    {
        var (context, _, mockHubContext) = CreateEnvironment();
        var plantsController = new PlantsController(context, mockHubContext.Object);

        var plantUser1 = CreatePlantForUser(context, userId: 1, "Монстера Користувача 1");
        var plantUser2 = CreatePlantForUser(context, userId: 2, "Фікус Користувача 2");

        SetUserContext(plantsController, 1, "user_one");
        var result = await plantsController.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var userPlants = Assert.IsAssignableFrom<IEnumerable<PlantResponseDto>>(okResult.Value)
            .ToList();

        Assert.Single(userPlants);
        Assert.Equal(plantUser1.Id, userPlants[0].Id);
        Assert.DoesNotContain(userPlants, plant => plant.Id == plantUser2.Id);

        var waterResult = await plantsController.WaterPlant(plantUser2.Id);
        Assert.IsType<NotFoundObjectResult>(waterResult);
    }

    /// <summary>
    /// Перевіряє, що власник може змінювати та видаляти рослину, а інші користувачі не мають такого доступу.
    /// </summary>
    [Fact]
    public async Task Plants_UpdateAndDelete_WorksForOwnerAndBlockedForOthers()
    {
        var (context, _, mockHubContext) = CreateEnvironment();
        var plantsController = new PlantsController(context, mockHubContext.Object);
        var plant = CreatePlantForUser(context, userId: 1, "Початкова назва");
        var updateDto = new PlantUpdateDto("Нове ім'я", "Species", 40, 80, 20, 30, 500);

        // Чужий користувач не може змінити або видалити рослину.
        SetUserContext(plantsController, 2, "user_two");
        var foreignUpdateResult = await plantsController.Update(plant.Id, updateDto);
        Assert.IsType<NotFoundObjectResult>(foreignUpdateResult.Result);

        var foreignDeleteResult = await plantsController.Delete(plant.Id);
        Assert.IsType<NotFoundObjectResult>(foreignDeleteResult);

        // Власник може оновити рослину та видалити її.
        SetUserContext(plantsController, 1, "user_one");
        var ownUpdateResult = await plantsController.Update(plant.Id, updateDto);
        var okUpdate = Assert.IsType<OkObjectResult>(ownUpdateResult.Result);
        var updatedPlant = Assert.IsType<PlantResponseDto>(okUpdate.Value);
        Assert.Equal("Нове ім'я", updatedPlant.Name);

        var ownDeleteResult = await plantsController.Delete(plant.Id);
        Assert.IsType<NoContentResult>(ownDeleteResult);

        Assert.Null(await context.Plants.FindAsync(plant.Id));
    }

    // =========================================================================
    // ТЕСТИ СПОВІЩЕНЬ
    // =========================================================================

    /// <summary>
    /// Перевіряє, що користувач отримує та закриває лише власні сповіщення.
    /// </summary>
    [Fact]
    public async Task Alerts_DataIsolation_UserOnlyReceivesAndResolvesOwnAlerts()
    {
        var (context, _, _) = CreateEnvironment();
        var alertsController = new AlertsController(context);

        var plantUser1 = CreatePlantForUser(context, userId: 1);
        var plantUser2 = CreatePlantForUser(context, userId: 2);

        var alert1 = new Alert
        {
            PlantId = plantUser1.Id,
            Type = AlertType.LowMoisture,
            Message = "Сповіщення 1",
            IsResolved = false
        };
        var alert2 = new Alert
        {
            PlantId = plantUser2.Id,
            Type = AlertType.HighTemperature,
            Message = "Сповіщення 2",
            IsResolved = false
        };

        context.Alerts.AddRange(alert1, alert2);
        await context.SaveChangesAsync();

        SetUserContext(alertsController, 1, "user_one");
        var activeAlertsResult = await alertsController.GetActiveAlerts();
        var okResult = Assert.IsType<OkObjectResult>(activeAlertsResult.Result);
        var userAlerts = Assert.IsAssignableFrom<IEnumerable<AlertResponseDto>>(okResult.Value)
            .ToList();

        Assert.Single(userAlerts);
        Assert.Equal(alert1.Id, userAlerts[0].Id);

        var resolveForeignResult = await alertsController.ResolveAlert(alert2.Id);
        Assert.IsType<NotFoundObjectResult>(resolveForeignResult);

        var resolveOwnResult = await alertsController.ResolveAlert(alert1.Id);
        Assert.IsType<NoContentResult>(resolveOwnResult);

        var updatedAlert = await context.Alerts.FindAsync(alert1.Id);
        Assert.True(updatedAlert!.IsResolved);
    }

    // =========================================================================
    // ТЕСТИ SIGNALR
    // =========================================================================

    /// <summary>
    /// Перевіряє надсилання сповіщення через SignalR саме для користувача — власника рослини.
    /// </summary>
    [Fact]
    public async Task WebSockets_BroadcastsAlertToTargetUserOnly()
    {
        var (context, mockUserProxy, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);
        var plant = CreatePlantForUser(context, userId: 42);

        SetDeviceKeyHeader(telemetryController, plant.ApiKey);
        var criticalTelemetry = new TelemetryCreateDto(
            Moisture: 10.0f,
            Temperature: 22.0f,
            Light: 400.0f);

        await telemetryController.PostTelemetry(criticalTelemetry);

        mockUserProxy.Verify(
            client => client.SendCoreAsync(
                "ReceiveAlert",
                It.IsAny<object[]>(),
                default),
            Times.Once);

        var alert = await context.Alerts
            .FirstOrDefaultAsync(item => item.PlantId == plant.Id);

        Assert.NotNull(alert);
        Assert.Equal(AlertType.LowMoisture, alert.Type);
    }

    // =========================================================================
    // ТЕСТИ КЕРУВАННЯ КОРИСТУВАЧАМИ ТА АВТЕНТИФІКАЦІЇ
    // =========================================================================

    /// <summary>
    /// Перевіряє реєстрацію користувача, вхід із правильним паролем і відхилення неправильного пароля.
    /// </summary>
    [Fact]
    public async Task Users_RegistrationAndLogin_IssuesValidToken()
    {
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        var registerResult = await usersController.Register(
            new UserRegisterDto("gardener_1", "secure_pass_123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(registerResult.Result);
        var registeredUser = Assert.IsType<UserResponseDto>(createdResult.Value);

        Assert.Equal("gardener_1", registeredUser.Username);
        Assert.False(string.IsNullOrWhiteSpace(registeredUser.Token));

        var loginResult = await usersController.Login(
            new UserLoginDto("gardener_1", "secure_pass_123"));
        var loginOk = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loggedUser = Assert.IsType<UserResponseDto>(loginOk.Value);

        Assert.Equal(registeredUser.Id, loggedUser.Id);
        Assert.False(string.IsNullOrWhiteSpace(loggedUser.Token));

        var badLoginResult = await usersController.Login(
            new UserLoginDto("gardener_1", "wrong_password"));
        Assert.IsType<UnauthorizedObjectResult>(badLoginResult.Result);
    }

    /// <summary>
    /// Перевіряє захист методу отримання профілю від запитів без автентифікованого користувача.
    /// </summary>
    [Fact]
    public async Task Users_Authorization_ProtectsGetMeEndpoint()
    {
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        var registerResult = await usersController.Register(
            new UserRegisterDto("auth_user", "mypassword"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(registerResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        usersController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var unauthorizedResult = await usersController.GetMe();
        Assert.IsType<UnauthorizedObjectResult>(unauthorizedResult.Result);

        SetUserContext(usersController, user.Id, user.Username);
        var authorizedResult = await usersController.GetMe();
        var okResult = Assert.IsType<OkObjectResult>(authorizedResult.Result);
        var profile = Assert.IsType<UserResponseDto>(okResult.Value);

        Assert.Equal(user.Id, profile.Id);
        Assert.Equal("auth_user", profile.Username);
    }

    /// <summary>
    /// Перевіряє повернення 404, якщо профіль із вказаним ідентифікатором уже відсутній у базі даних.
    /// </summary>
    [Fact]
    public async Task Users_GetMe_ReturnsNotFoundForDeletedUser()
    {
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        var registerResult = await usersController.Register(
            new UserRegisterDto("removed_user", "password123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(registerResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        SetUserContext(usersController, user.Id, user.Username);
        var dbUser = await context.Users.FindAsync(user.Id);
        Assert.NotNull(dbUser);

        context.Users.Remove(dbUser);
        await context.SaveChangesAsync();

        var result = await usersController.GetMe();
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    /// <summary>
    /// Перевіряє зміну пароля, відхилення неправильного старого пароля та вхід із новим паролем.
    /// </summary>
    [Fact]
    public async Task Users_ChangePassword_WorksCorrectlyAndRejectsWrongOldPassword()
    {
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        var registerResult = await usersController.Register(
            new UserRegisterDto("pass_user", "old_secret_123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(registerResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        SetUserContext(usersController, user.Id, user.Username);

        var wrongOldPasswordResult = await usersController.ChangePassword(
            new UserChangePasswordDto("incorrect_old_pass", "new_secret_456"));
        Assert.IsType<BadRequestObjectResult>(wrongOldPasswordResult);

        var changePasswordResult = await usersController.ChangePassword(
            new UserChangePasswordDto("old_secret_123", "new_secret_456"));
        Assert.IsType<OkObjectResult>(changePasswordResult);

        var oldLoginResult = await usersController.Login(
            new UserLoginDto("pass_user", "old_secret_123"));
        Assert.IsType<UnauthorizedObjectResult>(oldLoginResult.Result);

        var newLoginResult = await usersController.Login(
            new UserLoginDto("pass_user", "new_secret_456"));
        Assert.IsType<OkObjectResult>(newLoginResult.Result);
    }

    /// <summary>
    /// Перевіряє видалення облікового запису, пов'язаних рослин і подальшу недоступність профілю.
    /// </summary>
    [Fact]
    public async Task Users_DeleteMe_RemovesUserAndPlants_AndTokenBecomesInvalid()
    {
        var (context, _, mockHubContext) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);
        var plantsController = new PlantsController(context, mockHubContext.Object);

        var registerResult = await usersController.Register(
            new UserRegisterDto("deleted_user", "password123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(registerResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        SetUserContext(plantsController, user.Id, user.Username);
        var plantResult = await plantsController.Create(
            new PlantCreateDto("Рослина користувача", "Species"));
        var createdPlant = Assert.IsType<CreatedAtActionResult>(plantResult.Result);
        var plant = Assert.IsType<PlantResponseDto>(createdPlant.Value);

        SetUserContext(usersController, user.Id, user.Username);
        var deleteResult = await usersController.DeleteMe();
        Assert.IsType<NoContentResult>(deleteResult);

        var dbUser = await context.Users.FindAsync(user.Id);
        var dbPlant = await context.Plants.FindAsync(plant.Id);
        Assert.Null(dbUser);
        Assert.Null(dbPlant);

        var invalidTokenResult = await usersController.GetMe();
        Assert.IsType<NotFoundObjectResult>(invalidTokenResult.Result);
    }

    /// <summary>
    /// Перевіряє відхилення повторної реєстрації з уже зайнятим ім'ям користувача.
    /// </summary>
    [Fact]
    public async Task Users_Registration_DuplicateUsername_ReturnsBadRequestOrConflict()
    {
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        await usersController.Register(
            new UserRegisterDto("duplicate_user", "password123"));

        var secondAttempt = await usersController.Register(
            new UserRegisterDto("duplicate_user", "another_password"));

        Assert.True(
            secondAttempt.Result is BadRequestObjectResult
                || secondAttempt.Result is ConflictObjectResult,
            "Повторна реєстрація з наявним ім'ям користувача має бути відхилена.");
    }

    /// <summary>
    /// Додатково перевіряє, що повторна реєстрація не створює другого користувача з таким самим ім'ям.
    /// </summary>
    [Fact]
    public async Task Users_Registration_DuplicateUsername_DoesNotCreateSecondUser()
    {
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        await usersController.Register(
            new UserRegisterDto("unique_user", "password123"));
        await usersController.Register(
            new UserRegisterDto("unique_user", "another_password"));

        var users = await context.Users
            .Where(user => user.Username == "unique_user")
            .ToListAsync();

        Assert.Single(users);
    }
}