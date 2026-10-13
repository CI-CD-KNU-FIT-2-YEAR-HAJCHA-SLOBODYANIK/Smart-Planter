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

        // Инициализация конфигурации в памяти для эмитации appsettings.json (нужна для сборки и проверки JWT)
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
    /// Создает изолированное тестовое окружение с In-Memory базой данных и заглушками для SignalR Hub.
    /// </summary>
    private (AppDbContext Context, Mock<IClientProxy> MockUserProxy, Mock<IHubContext<TelemetryHub>> MockHubContext) CreateEnvironment()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);

        var mockHubClients = new Mock<IHubClients>();
        var mockUserProxy = new Mock<IClientProxy>();

        // Настройка мока: при пересылке адресных сообщений конкретному UserId возвращаем MockUserProxy
        mockHubClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockUserProxy.Object);

        var mockHubContext = new Mock<IHubContext<TelemetryHub>>();
        mockHubContext.Setup(h => h.Clients).Returns(mockHubClients.Object);

        return (context, mockUserProxy, mockHubContext);
    }

    /// <summary>
    /// Устанавливает контекст авторизованного пользователя (Claims/JWT identity) в HttpContext контроллера.
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

        if (controller.ControllerContext.HttpContext == null)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        controller.ControllerContext.HttpContext.User = principal;
    }

    /// <summary>
    /// Устанавливает или очищает заголовок 'X-Device-Key' в HTTP-запросе для аутентификации IoT-устройств.
    /// </summary>
    private static void SetDeviceKeyHeader(ControllerBase controller, string? deviceKey)
    {
        if (controller.ControllerContext.HttpContext == null)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

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
    /// Вспомогательный метод создания растения напрямую в базе данных с привязкой к UserId.
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

    [Fact]
    public async Task Telemetry_DeviceApiKey_Authentication_AcceptsValidAndRejectsInvalid()
    {
        // Проверка аутентификации IoT-датчиков: отклонение запросов без ключа или с неверным ключом, прием с валидным.
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);

        var plant = CreatePlantForUser(context, userId: 1);

        // 1. Попытка отправки без ключа в заголовке X-Device-Key -> ожидаем 401 Unauthorized
        SetDeviceKeyHeader(telemetryController, null);
        var noKeyResult = await telemetryController.PostTelemetry(new TelemetryCreateDto(50, 22, 400));
        Assert.IsType<UnauthorizedObjectResult>(noKeyResult.Result);

        // 2. Попытка отправки с невалидным ключом -> ожидаем 401 Unauthorized
        SetDeviceKeyHeader(telemetryController, "invalid_device_key_123");
        var invalidKeyResult = await telemetryController.PostTelemetry(new TelemetryCreateDto(50, 22, 400));
        Assert.IsType<UnauthorizedObjectResult>(invalidKeyResult.Result);

        // 3. Успешная отправка с валидным ключом ApiKey -> ожидаем 200 OK
        SetDeviceKeyHeader(telemetryController, plant.ApiKey);
        var successResult = await telemetryController.PostTelemetry(new TelemetryCreateDto(50, 22, 400));
        var okResult = Assert.IsType<OkObjectResult>(successResult.Result);
        var response = Assert.IsType<TelemetryResponseDto>(okResult.Value);

        Assert.Equal(plant.Id, response.PlantId);
        Assert.Equal(50, response.Moisture);
    }

    [Fact]
    public async Task Plants_DataIsolation_UserCannotAccessOtherUsersPlants()
    {
        // Проверка изоляции данных: пользователь А не видит и не может полить растения пользователя Б.
        var (context, _, mockHubContext) = CreateEnvironment();
        var plantsController = new PlantsController(context, mockHubContext.Object);

        var plantUser1 = CreatePlantForUser(context, userId: 1, "Монстера Користувача 1");
        var plantUser2 = CreatePlantForUser(context, userId: 2, "Фікус Користувача 2");

        // Входим под пользователем 1
        SetUserContext(plantsController, 1, "user_one");

        // Вызов GET /plants должен вернуть только растения пользователя 1
        var result = await plantsController.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var user1Plants = Assert.IsAssignableFrom<IEnumerable<PlantResponseDto>>(okResult.Value).ToList();

        Assert.Single(user1Plants);
        Assert.Equal(plantUser1.Id, user1Plants[0].Id);
        Assert.DoesNotContain(user1Plants, p => p.Id == plantUser2.Id);

        // Попытка полить чужое растение (User 2) -> должна вернуться ошибка 404 Not Found
        var waterResult = await plantsController.WaterPlant(plantUser2.Id);
        Assert.IsType<NotFoundObjectResult>(waterResult);
    }

    [Fact]
    public async Task Plants_UpdateAndDelete_WorksForOwnerAndBlockedForOthers()
    {
        // Проверка редактирования и удаления растений с контролем прав владения.
        var (context, _, mockHubContext) = CreateEnvironment();
        var plantsController = new PlantsController(context, mockHubContext.Object);

        var plantUser1 = CreatePlantForUser(context, userId: 1, "Исходное название");

        // 1. Входим под чужим пользователем (User 2) и пытаемся изменить/удалить растение User 1 -> 404 Not Found
        SetUserContext(plantsController, 2, "user_two");
        var updateDto = new PlantUpdateDto("Новое имя", "Species", 40, 80, 20, 30, 500);

        var foreignUpdateResult = await plantsController.Update(plantUser1.Id, updateDto);
        Assert.IsType<NotFoundObjectResult>(foreignUpdateResult.Result);

        var foreignDeleteResult = await plantsController.Delete(plantUser1.Id);
        Assert.IsType<NotFoundObjectResult>(foreignDeleteResult);

        // 2. Входим под владельцем (User 1) и обновляем параметры -> 200 OK
        SetUserContext(plantsController, 1, "user_one");
        var ownUpdateResult = await plantsController.Update(plantUser1.Id, updateDto);
        var okUpdate = Assert.IsType<OkObjectResult>(ownUpdateResult.Result);
        var updatedPlant = Assert.IsType<PlantResponseDto>(okUpdate.Value);
        Assert.Equal("Новое имя", updatedPlant.Name);

        // 3. Удаляем собственное растение -> 204 NoContent
        var ownDeleteResult = await plantsController.Delete(plantUser1.Id);
        Assert.IsType<NoContentResult>(ownDeleteResult);

        // Проверяем, что растение действительно удалено из базы
        var dbPlant = await context.Plants.FindAsync(plantUser1.Id);
        Assert.Null(dbPlant);
    }

    [Fact]
    public async Task Telemetry_History_DataIsolation_UserCannotViewForeignPlantHistory()
    {
        // Проверка изоляции истории замеров: запрет просмотра истории чужого растения.
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

        // Авторизуемся под User 1 и пытаемся запросить историю растения User 2 -> 404 Not Found
        SetUserContext(telemetryController, 1, "user_one");
        var result = await telemetryController.GetHistory(plantUser2.Id);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Alerts_DataIsolation_UserOnlyReceivesAndResolvesOwnAlerts()
    {
        // Проверка изоляции алертов: получение и разрешение только личных алертов.
        var (context, _, _) = CreateEnvironment();
        var alertsController = new AlertsController(context);

        var plantUser1 = CreatePlantForUser(context, userId: 1);
        var plantUser2 = CreatePlantForUser(context, userId: 2);

        var alert1 = new Alert { PlantId = plantUser1.Id, Type = AlertType.LowMoisture, Message = "Алерт 1", IsResolved = false };
        var alert2 = new Alert { PlantId = plantUser2.Id, Type = AlertType.HighTemperature, Message = "Алерт 2", IsResolved = false };
        context.Alerts.AddRange(alert1, alert2);
        await context.SaveChangesAsync();

        SetUserContext(alertsController, 1, "user_one");

        // 1. GET /alerts/active должен вернуть только активный алерт растения User 1
        var activeAlertsResult = await alertsController.GetActiveAlerts();
        var okResult = Assert.IsType<OkObjectResult>(activeAlertsResult.Result);
        var alerts = Assert.IsAssignableFrom<IEnumerable<AlertResponseDto>>(okResult.Value).ToList();

        Assert.Single(alerts);
        Assert.Equal(alert1.Id, alerts[0].Id);

        // 2. Попытка разрешить чужой алерт (User 2) -> 404 Not Found
        var resolveForeignResult = await alertsController.ResolveAlert(alert2.Id);
        Assert.IsType<NotFoundObjectResult>(resolveForeignResult);

        // 3. Разрешение своего алерта (User 1) -> 204 NoContent
        var resolveOwnResult = await alertsController.ResolveAlert(alert1.Id);
        Assert.IsType<NoContentResult>(resolveOwnResult);

        var updatedAlert1 = await context.Alerts.FindAsync(alert1.Id);
        Assert.True(updatedAlert1!.IsResolved);
    }

    [Fact]
    public async Task WebSockets_BroadcastsAlertToTargetUserOnly()
    {
        // Проверка адресной WebSocket-рассылки: сокет-событие отправляется строго в канал владельца растения (Clients.User(userId)).
        var (context, mockUserProxy, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);

        var plantUser1 = CreatePlantForUser(context, userId: 42);

        // Имитируем критическую телеметрию с датчика (низкая влажность)
        SetDeviceKeyHeader(telemetryController, plantUser1.ApiKey);
        var badTelemetry = new TelemetryCreateDto(Moisture: 10.0f, Temperature: 22.0f, Light: 400.0f);

        await telemetryController.PostTelemetry(badTelemetry);

        // Проверяем, что вызов SendCoreAsync для 'ReceiveAlert' отправлен ровно один раз в адресный сокет пользователя "42"
        mockUserProxy.Verify(
            c => c.SendCoreAsync("ReceiveAlert", It.IsAny<object[]>(), default),
            Times.Once);

        var alert = await context.Alerts.FirstOrDefaultAsync(a => a.PlantId == plantUser1.Id);
        Assert.NotNull(alert);
        Assert.Equal(AlertType.LowMoisture, alert.Type);
    }

    [Fact]
    public async Task Telemetry_HistoryFiltering_WorksWithDatesAndDays()
    {
        // Проверка фильтрации истории по временно́му отрезку (за последние N дней).
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);

        var plant = CreatePlantForUser(context, userId: 1);
        SetUserContext(telemetryController, 1, "user_one");

        // Добавляем 3 записи с разным временем
        context.Telemetries.Add(new Telemetry { PlantId = plant.Id, Moisture = 50, Temperature = 22, Light = 300, Timestamp = DateTime.UtcNow.AddDays(-10) });
        context.Telemetries.Add(new Telemetry { PlantId = plant.Id, Moisture = 50, Temperature = 22, Light = 300, Timestamp = DateTime.UtcNow.AddDays(-2) });
        context.Telemetries.Add(new Telemetry { PlantId = plant.Id, Moisture = 50, Temperature = 22, Light = 300, Timestamp = DateTime.UtcNow });
        await context.SaveChangesAsync();

        // Запрашиваем историю за последние 5 дней -> ожидаем ровно 2 новые записи (отсекается 10-дневная)
        var result = await telemetryController.GetHistory(plant.Id, limit: 10, days: 5);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<TelemetryResponseDto>>(okResult.Value).ToList();

        Assert.Equal(2, list.Count);
        Assert.True(list[0].Timestamp >= list[1].Timestamp); // Проверка сортировки от новых к старым
    }

    [Fact]
    public async Task Users_RegistrationAndLogin_IssuesValidToken()
    {
        // Проверка полного цикла аутентификации: регистрация, успешный вход и валидация пароля.
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        // 1. Регистрация нового пользователя -> 201 Created и выдача токена
        var regResult = await usersController.Register(new UserRegisterDto("gardener_1", "secure_pass_123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var registeredUser = Assert.IsType<UserResponseDto>(createdResult.Value);

        Assert.Equal("gardener_1", registeredUser.Username);
        Assert.False(string.IsNullOrWhiteSpace(registeredUser.Token));

        // 2. Логин с верным паролем -> 200 OK
        var loginResult = await usersController.Login(new UserLoginDto("gardener_1", "secure_pass_123"));
        var loginOk = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loggedUser = Assert.IsType<UserResponseDto>(loginOk.Value);

        Assert.Equal(registeredUser.Id, loggedUser.Id);
        Assert.False(string.IsNullOrWhiteSpace(loggedUser.Token));

        // 3. Попытка входа с ошибочным паролем -> 401 Unauthorized
        var badLoginResult = await usersController.Login(new UserLoginDto("gardener_1", "wrong_password"));
        Assert.IsType<UnauthorizedObjectResult>(badLoginResult.Result);
    }

    [Fact]
    public async Task Users_Authorization_ProtectsGetMeEndpoint()
    {
        // Проверка защиты эндпоинта /users/me атрибутом [Authorize].
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        var regResult = await usersController.Register(new UserRegisterDto("auth_user", "mypassword"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        // 1. Вызов без заголовков авторизации -> 401 Unauthorized
        usersController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var unauthorizedResult = await usersController.GetMe();
        Assert.IsType<UnauthorizedObjectResult>(unauthorizedResult.Result);

        // 2. Вызов с установленным контекстом пользователя -> 200 OK
        SetUserContext(usersController, user.Id, user.Username);
        var authorizedResult = await usersController.GetMe();
        var okResult = Assert.IsType<OkObjectResult>(authorizedResult.Result);
        var profile = Assert.IsType<UserResponseDto>(okResult.Value);

        Assert.Equal(user.Id, profile.Id);
        Assert.Equal("auth_user", profile.Username);
    }

    [Fact]
    public async Task Users_ChangePassword_WorksCorrectlyAndRejectsWrongOldPassword()
    {
        // Проверка функции смены пароля пользователя.
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        var regResult = await usersController.Register(new UserRegisterDto("pass_user", "old_secret_123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        SetUserContext(usersController, user.Id, user.Username);

        // 1. Передача неверного старого пароля -> 400 Bad Request
        var wrongOldPassResult = await usersController.ChangePassword(new UserChangePasswordDto("incorrect_old_pass", "new_secret_456"));
        Assert.IsType<BadRequestObjectResult>(wrongOldPassResult);

        // 2. Успешная смена пароля с верным старым паролем -> 200 OK
        var successChangeResult = await usersController.ChangePassword(new UserChangePasswordDto("old_secret_123", "new_secret_456"));
        Assert.IsType<OkObjectResult>(successChangeResult);

        // 3. Вход со старым паролем должен отклоняться -> 401 Unauthorized
        var oldLoginResult = await usersController.Login(new UserLoginDto("pass_user", "old_secret_123"));
        Assert.IsType<UnauthorizedObjectResult>(oldLoginResult.Result);

        // 4. Вход с новым паролем должен проходить успешно -> 200 OK
        var newLoginResult = await usersController.Login(new UserLoginDto("pass_user", "new_secret_456"));
        Assert.IsType<OkObjectResult>(newLoginResult.Result);
    }

    [Fact]
    public async Task Users_DeleteMe_RemovesUserAndPlants_AndTokenBecomesInvalid()
    {
        // Проверка удаления профиля пользователем и аннулирования его токена/сессии при поиске в БД.
        var (context, _, mockHubContext) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);
        var plantsController = new PlantsController(context, mockHubContext.Object);

        // 1. Регистрируем пользователя и создаем для него растение
        var regResult = await usersController.Register(new UserRegisterDto("deleted_user", "password123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        SetUserContext(plantsController, user.Id, user.Username);
        var plantResult = await plantsController.Create(new PlantCreateDto("Растение удаляемого юзера", "Species"));
        var createdPlant = Assert.IsType<CreatedAtActionResult>(plantResult.Result);
        var plant = Assert.IsType<PlantResponseDto>(createdPlant.Value);

        // 2. Авторизуемся под созданным пользователем и вызываем DELETE /users/me
        SetUserContext(usersController, user.Id, user.Username);
        var deleteResult = await usersController.DeleteMe();
        Assert.IsType<NoContentResult>(deleteResult);

        // 3. Проверяем, что пользователь и его растения каскадно удалились из базы данных
        var dbUser = await context.Users.FindAsync(user.Id);
        var dbPlant = await context.Plants.FindAsync(plant.Id);
        Assert.Null(dbUser);
        Assert.Null(dbPlant);

        // 4. Проверка инвалидации токена: при попытке вызвать /users/me с ранее выданными клеймами/токеном
        // контроллер проверяет наличие пользователя в БД и возвращает 404 Not Found (пользователь удален)
        var invalidTokenResult = await usersController.GetMe();
        Assert.IsType<NotFoundObjectResult>(invalidTokenResult.Result);
    }
}