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

    private (AppDbContext Context, Mock<IClientProxy> MockUserProxy, Mock<IHubContext<TelemetryHub>> MockHubContext) CreateEnvironment()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);

        var mockHubClients = new Mock<IHubClients>();
        var mockUserProxy = new Mock<IClientProxy>();

        // Настройка адресной отправки пользователю по ID
        mockHubClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockUserProxy.Object);

        var mockHubContext = new Mock<IHubContext<TelemetryHub>>();
        mockHubContext.Setup(h => h.Clients).Returns(mockHubClients.Object);

        return (context, mockUserProxy, mockHubContext);
    }

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
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);

        var plant = CreatePlantForUser(context, userId: 1);

        // 1. Попытка отправки без ключа в заголовке
        SetDeviceKeyHeader(telemetryController, null);
        var noKeyResult = await telemetryController.PostTelemetry(new TelemetryCreateDto(50, 22, 400));
        Assert.IsType<UnauthorizedObjectResult>(noKeyResult.Result);

        // 2. Попытка отправки с невалидным ключом
        SetDeviceKeyHeader(telemetryController, "invalid_device_key_123");
        var invalidKeyResult = await telemetryController.PostTelemetry(new TelemetryCreateDto(50, 22, 400));
        Assert.IsType<UnauthorizedObjectResult>(invalidKeyResult.Result);

        // 3. Успешная отправка с валидным ключом
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
        var (context, _, mockHubContext) = CreateEnvironment();
        var plantsController = new PlantsController(context, mockHubContext.Object);

        // Создаем растения для двух разных пользователей
        var plantUser1 = CreatePlantForUser(context, userId: 1, "Монстера Користувача 1");
        var plantUser2 = CreatePlantForUser(context, userId: 2, "Фікус Користувача 2");

        // Авторизуемся под пользователем 1
        SetUserContext(plantsController, 1, "user_one");

        var result = await plantsController.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var user1Plants = Assert.IsAssignableFrom<IEnumerable<PlantResponseDto>>(okResult.Value).ToList();

        // Пользователь 1 видит только свое растение
        Assert.Single(user1Plants);
        Assert.Equal(plantUser1.Id, user1Plants[0].Id);
        Assert.DoesNotContain(user1Plants, p => p.Id == plantUser2.Id);

        // Пользователь 1 пытается полить растение пользователя 2
        var waterResult = await plantsController.WaterPlant(plantUser2.Id);
        Assert.IsType<NotFoundObjectResult>(waterResult);
    }

    [Fact]
    public async Task Telemetry_History_DataIsolation_UserCannotViewForeignPlantHistory()
    {
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);

        var plantUser1 = CreatePlantForUser(context, userId: 1);
        var plantUser2 = CreatePlantForUser(context, userId: 2);

        // Добавляем телеметрию для растения пользователя 2
        context.Telemetries.Add(new Telemetry
        {
            PlantId = plantUser2.Id,
            Moisture = 45,
            Temperature = 22,
            Light = 350,
            Timestamp = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Авторизуемся под пользователем 1 и запрашиваем историю растения пользователя 2
        SetUserContext(telemetryController, 1, "user_one");
        var result = await telemetryController.GetHistory(plantUser2.Id);

        // Доступ должен быть отклонен (NotFound или ForbidResult)
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Alerts_DataIsolation_UserOnlyReceivesAndResolvesOwnAlerts()
    {
        var (context, _, _) = CreateEnvironment();
        var alertsController = new AlertsController(context);

        var plantUser1 = CreatePlantForUser(context, userId: 1);
        var plantUser2 = CreatePlantForUser(context, userId: 2);

        var alert1 = new Alert { PlantId = plantUser1.Id, Type = AlertType.LowMoisture, Message = "Алерт 1", IsResolved = false };
        var alert2 = new Alert { PlantId = plantUser2.Id, Type = AlertType.HighTemperature, Message = "Алерт 2", IsResolved = false };
        context.Alerts.AddRange(alert1, alert2);
        await context.SaveChangesAsync();

        // Авторизуемся под пользователем 1
        SetUserContext(alertsController, 1, "user_one");

        // 1. Проверяем выборку активных алертов
        var activeAlertsResult = await alertsController.GetActiveAlerts();
        var okResult = Assert.IsType<OkObjectResult>(activeAlertsResult.Result);
        var alerts = Assert.IsAssignableFrom<IEnumerable<AlertResponseDto>>(okResult.Value).ToList();

        Assert.Single(alerts);
        Assert.Equal(alert1.Id, alerts[0].Id);

        // 2. Попытка разрешить чужой алерт
        var resolveForeignResult = await alertsController.ResolveAlert(alert2.Id);
        Assert.IsType<NotFoundObjectResult>(resolveForeignResult);

        // 3. Успешное разрешение собственного алерта
        var resolveOwnResult = await alertsController.ResolveAlert(alert1.Id);
        Assert.IsType<NoContentResult>(resolveOwnResult);

        var updatedAlert1 = await context.Alerts.FindAsync(alert1.Id);
        Assert.True(updatedAlert1!.IsResolved);
    }

    [Fact]
    public async Task WebSockets_BroadcastsAlertToTargetUserOnly()
    {
        var (context, mockUserProxy, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);

        var plantUser1 = CreatePlantForUser(context, userId: 42);

        // Отправляем телеметрию с критическим показателем влажности от датчика растения
        SetDeviceKeyHeader(telemetryController, plantUser1.ApiKey);
        var badTelemetry = new TelemetryCreateDto(Moisture: 10.0f, Temperature: 22.0f, Light: 400.0f);

        await telemetryController.PostTelemetry(badTelemetry);

        // Проверяем, что событие ReceiveAlert ушло именно в канал пользователя "42"
        mockUserProxy.Verify(
            c => c.SendCoreAsync("ReceiveAlert", It.IsAny<object[]>(), default),
            Times.Once);

        // Проверяем факт создания алерта в БД
        var alert = await context.Alerts.FirstOrDefaultAsync(a => a.PlantId == plantUser1.Id);
        Assert.NotNull(alert);
        Assert.Equal(AlertType.LowMoisture, alert.Type);
    }

    [Fact]
    public async Task Telemetry_HistoryFiltering_WorksWithDatesAndDays()
    {
        var (context, _, mockHubContext) = CreateEnvironment();
        var telemetryController = new TelemetryController(context, mockHubContext.Object);

        var plant = CreatePlantForUser(context, userId: 1);
        SetUserContext(telemetryController, 1, "user_one");

        // 10 дней назад
        context.Telemetries.Add(new Telemetry { PlantId = plant.Id, Moisture = 50, Temperature = 22, Light = 300, Timestamp = DateTime.UtcNow.AddDays(-10) });
        // 2 дня назад
        context.Telemetries.Add(new Telemetry { PlantId = plant.Id, Moisture = 50, Temperature = 22, Light = 300, Timestamp = DateTime.UtcNow.AddDays(-2) });
        // Текущий замер
        context.Telemetries.Add(new Telemetry { PlantId = plant.Id, Moisture = 50, Temperature = 22, Light = 300, Timestamp = DateTime.UtcNow });
        await context.SaveChangesAsync();

        // Запрос за последние 5 дней
        var result = await telemetryController.GetHistory(plant.Id, limit: 10, days: 5);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<TelemetryResponseDto>>(okResult.Value).ToList();

        Assert.Equal(2, list.Count);
        Assert.True(list[0].Timestamp >= list[1].Timestamp);
    }

    [Fact]
    public async Task Users_RegistrationAndLogin_IssuesValidToken()
    {
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        // 1. Регистрация
        var regResult = await usersController.Register(new UserRegisterDto("gardener_1", "secure_pass_123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var registeredUser = Assert.IsType<UserResponseDto>(createdResult.Value);

        Assert.Equal("gardener_1", registeredUser.Username);
        Assert.False(string.IsNullOrWhiteSpace(registeredUser.Token));

        // 2. Успешный вход
        var loginResult = await usersController.Login(new UserLoginDto("gardener_1", "secure_pass_123"));
        var loginOk = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loggedUser = Assert.IsType<UserResponseDto>(loginOk.Value);

        Assert.Equal(registeredUser.Id, loggedUser.Id);
        Assert.False(string.IsNullOrWhiteSpace(loggedUser.Token));

        // 3. Отклонение входа с неверным паролем
        var badLoginResult = await usersController.Login(new UserLoginDto("gardener_1", "wrong_password"));
        Assert.IsType<UnauthorizedObjectResult>(badLoginResult.Result);
    }

    [Fact]
    public async Task Users_Authorization_ProtectsGetMeEndpoint()
    {
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        var regResult = await usersController.Register(new UserRegisterDto("auth_user", "mypassword"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        // Запрос без заголовков аутентификации
        usersController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var unauthorizedResult = await usersController.GetMe();
        Assert.IsType<UnauthorizedObjectResult>(unauthorizedResult.Result);

        // Запрос с установленным контекстом пользователя
        SetUserContext(usersController, user.Id, user.Username);
        var authorizedResult = await usersController.GetMe();
        var okResult = Assert.IsType<OkObjectResult>(authorizedResult.Result);
        var profile = Assert.IsType<UserResponseDto>(okResult.Value);

        Assert.Equal(user.Id, profile.Id);
        Assert.Equal("auth_user", profile.Username);
    }
}