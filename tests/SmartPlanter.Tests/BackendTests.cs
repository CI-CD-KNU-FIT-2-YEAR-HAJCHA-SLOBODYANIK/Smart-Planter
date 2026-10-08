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
    private const int BASE_TEST_COUNT = 1;
    private const int MASS_TEST_COUNT = 99;
    private const int SHUFFLE_POOL_COUNT = 100;
    private const int SHUFFLE_TAKE_COUNT = 10;
    private const int SHUFFLE_TELEMETRIES = 5;
    private const int COMBO_TEST_COUNT = 8;
    private const int SPAM_TEST_COUNT = 8;

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

    private (AppDbContext Context, TelemetryController Controller, Mock<IClientProxy> MockClients) CreateEnvironment()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);

        var mockHubClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockHubClients.Setup(c => c.All).Returns(mockClientProxy.Object);

        var mockHubContext = new Mock<IHubContext<TelemetryHub>>();
        mockHubContext.Setup(h => h.Clients).Returns(mockHubClients.Object);

        var controller = new TelemetryController(context, mockHubContext.Object);

        return (context, controller, mockClientProxy);
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

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private Plant GeneratePlant(int id)
    {
        return new Plant
        {
            Id = id,
            Name = $"Рослина {id}",
            Species = "Generated Species",
            MinMoisture = _random.Next(20, 40),
            MaxMoisture = _random.Next(60, 80),
            MinTemp = _random.Next(12, 18),
            MaxTemp = _random.Next(26, 34),
            MinLight = _random.Next(150, 300)
        };
    }

    private TelemetryCreateDto GenerateTelemetry(Plant plant, int badStateMask = 0, DateTime? timestamp = null)
    {
        double moisture = (badStateMask & 1) != 0
            ? plant.MinMoisture - 5.0
            : plant.MinMoisture + _random.NextDouble() * (plant.MaxMoisture - plant.MinMoisture);

        double temp = (badStateMask & 2) != 0
            ? plant.MinTemp - 2.0
            : plant.MinTemp + _random.NextDouble() * (plant.MaxTemp - plant.MinTemp);

        double light = (badStateMask & 4) != 0
            ? plant.MinLight - 50.0
            : plant.MinLight + _random.NextDouble() * 500.0;

        return new TelemetryCreateDto(plant.Id, (float)moisture, (float)temp, (float)light, timestamp);
    }

    [Fact]
    public async Task PlantCreation_SingleAndMass_MaintainsIntegrity()
    {
        var (context, _, _) = CreateEnvironment();

        var basePlant = GeneratePlant(1);
        context.Plants.Add(basePlant);
        await context.SaveChangesAsync();

        var dbBasePlant = await context.Plants.FindAsync(1);
        Assert.NotNull(dbBasePlant);
        Assert.Equal(basePlant.MinMoisture, dbBasePlant.MinMoisture);
        Assert.Equal(basePlant.MaxTemp, dbBasePlant.MaxTemp);

        for (int i = 2; i <= BASE_TEST_COUNT + MASS_TEST_COUNT; i++)
        {
            context.Plants.Add(GeneratePlant(i));
        }
        await context.SaveChangesAsync();

        var randomId = _random.Next(1, BASE_TEST_COUNT + MASS_TEST_COUNT + 1);
        var randomPlant = await context.Plants.FindAsync(randomId);
        Assert.NotNull(randomPlant);
        Assert.Equal($"Рослина {randomId}", randomPlant.Name);
        Assert.Equal(BASE_TEST_COUNT + MASS_TEST_COUNT, await context.Plants.CountAsync());
    }

    [Fact]
    public async Task Telemetry_ShuffleDistribution_SavesCorrectly()
    {
        var (context, controller, _) = CreateEnvironment();
        var plants = new List<Plant>();

        for (int i = 1; i <= SHUFFLE_POOL_COUNT; i++)
        {
            var p = GeneratePlant(i);
            plants.Add(p);
            context.Plants.Add(p);
        }
        await context.SaveChangesAsync();

        var shuffledIds = Enumerable.Range(2, SHUFFLE_POOL_COUNT - 1).OrderBy(_ => _random.Next()).ToArray();
        var selectedIds = shuffledIds.Take(SHUFFLE_TAKE_COUNT).ToArray();

        var telemetries = new List<TelemetryCreateDto>();
        foreach (var pId in selectedIds)
        {
            var plant = plants.First(p => p.Id == pId);
            for (int i = 0; i < SHUFFLE_TELEMETRIES; i++)
            {
                telemetries.Add(GenerateTelemetry(plant, 0));
            }
        }

        telemetries = telemetries.OrderBy(_ => _random.Next()).ToList();

        foreach (var t in telemetries) await controller.PostTelemetry(t);

        var testId = selectedIds[0];
        var result = await controller.GetHistory(testId);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var history = Assert.IsAssignableFrom<IEnumerable<TelemetryResponseDto>>(okResult.Value);

        Assert.Equal(SHUFFLE_TELEMETRIES, history.Count());
        Assert.All(history, h => Assert.Equal(testId, h.PlantId));
    }

    [Fact]
    public async Task Alerts_Combinations_GeneratesCorrectAlertTypes()
    {
        var (context, controller, _) = CreateEnvironment();
        var plants = new List<Plant>();

        for (int i = 1; i <= COMBO_TEST_COUNT; i++)
        {
            var p = GeneratePlant(i);
            plants.Add(p);
            context.Plants.Add(p);
        }
        await context.SaveChangesAsync();

        for (int mask = 0; mask < COMBO_TEST_COUNT; mask++)
        {
            int plantId = mask + 1;
            var targetPlant = plants.First(p => p.Id == plantId);
            await controller.PostTelemetry(GenerateTelemetry(targetPlant, mask));

            var alerts = await context.Alerts.Where(a => a.PlantId == plantId).ToListAsync();

            int expectedAlerts = 0;
            if ((mask & 1) != 0) expectedAlerts++;
            if ((mask & 2) != 0) expectedAlerts++;
            if ((mask & 4) != 0) expectedAlerts++;

            Assert.Equal(expectedAlerts, alerts.Count);
        }
    }

    [Fact]
    public async Task Alerts_DuplicationProtection_WithProbabilitySpam()
    {
        var (context, controller, _) = CreateEnvironment();
        var plants = new List<Plant>();

        for (int i = 1; i <= SPAM_TEST_COUNT; i++)
        {
            var p = GeneratePlant(i);
            plants.Add(p);
            context.Plants.Add(p);
        }
        await context.SaveChangesAsync();

        for (int mask = 0; mask < SPAM_TEST_COUNT; mask++)
        {
            int plantId = mask + 1;
            var targetPlant = plants.First(p => p.Id == plantId);

            for (int attempt = 1; attempt <= 5; attempt++)
            {
                double chance = attempt * 0.2;

                int currentMask = 0;
                if ((mask & 1) != 0 && _random.NextDouble() < chance) currentMask |= 1;
                if ((mask & 2) != 0 && _random.NextDouble() < chance) currentMask |= 2;
                if ((mask & 4) != 0 && _random.NextDouble() < chance) currentMask |= 4;

                await controller.PostTelemetry(GenerateTelemetry(targetPlant, currentMask));
            }

            var activeAlerts = await context.Alerts.Where(a => a.PlantId == plantId).ToListAsync();
            var uniqueAlertTypes = activeAlerts.Select(a => a.Type).Distinct().Count();
            Assert.Equal(activeAlerts.Count, uniqueAlertTypes);

            int expectedAlerts = 0;
            if ((mask & 1) != 0) expectedAlerts++;
            if ((mask & 2) != 0) expectedAlerts++;
            if ((mask & 4) != 0) expectedAlerts++;

            Assert.Equal(expectedAlerts, activeAlerts.Count);
        }
    }

    [Fact]
    public async Task Alert_Lifecycle_CreatesAndResolves()
    {
        var (context, controller, _) = CreateEnvironment();
        var plant1 = GeneratePlant(1);
        context.Plants.Add(plant1);
        await context.SaveChangesAsync();

        await controller.PostTelemetry(GenerateTelemetry(plant1, 1));

        var alert = await context.Alerts.FirstAsync();
        Assert.False(alert.IsResolved);

        alert.IsResolved = true;
        await context.SaveChangesAsync();

        await controller.PostTelemetry(GenerateTelemetry(plant1, 1));

        var alerts = await context.Alerts.Where(a => a.PlantId == plant1.Id).ToListAsync();
        Assert.Equal(2, alerts.Count);
        Assert.Single(alerts, a => !a.IsResolved);
    }

    [Fact]
    public async Task WebSockets_BroadcastsTelemetryData_AndAlerts()
    {
        var (context, controller, mockClients) = CreateEnvironment();
        var plant1 = GeneratePlant(1);
        context.Plants.Add(plant1);
        await context.SaveChangesAsync();

        // 1. Обычная телеметрия -> трансляция ReceiveTelemetry
        await controller.PostTelemetry(GenerateTelemetry(plant1, 0));
        mockClients.Verify(
            c => c.SendCoreAsync("ReceiveTelemetry", It.IsAny<object[]>(), default),
            Times.Once);

        // 2. Аварийная телеметрия -> трансляция ReceiveAlert
        await controller.PostTelemetry(GenerateTelemetry(plant1, 1));
        mockClients.Verify(
            c => c.SendCoreAsync("ReceiveAlert", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task Telemetry_HistoryFiltering_WorksWithDatesAndDays()
    {
        var (context, controller, _) = CreateEnvironment();
        var plant = GeneratePlant(1);
        context.Plants.Add(plant);
        await context.SaveChangesAsync();

        // Добавляем запись 10-дневной давности
        await controller.PostTelemetry(new TelemetryCreateDto(plant.Id, 50, 22, 300, DateTime.UtcNow.AddDays(-10)));
        // Добавляем запись 2-дневной давности
        await controller.PostTelemetry(new TelemetryCreateDto(plant.Id, 50, 22, 300, DateTime.UtcNow.AddDays(-2)));
        // Добавляем текущую запись
        await controller.PostTelemetry(new TelemetryCreateDto(plant.Id, 50, 22, 300, DateTime.UtcNow));

        // Выборка за последние 5 дней
        var result = await controller.GetHistory(plant.Id, limit: 10, days: 5);
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

        // 1. Регистрация нового пользователя
        var regResult = await usersController.Register(new UserRegisterDto("test_bot", "pass12345"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var registeredUser = Assert.IsType<UserResponseDto>(createdResult.Value);

        Assert.Equal("test_bot", registeredUser.Username);
        Assert.False(string.IsNullOrWhiteSpace(registeredUser.Token));

        // 2. Успешный вход
        var loginResult = await usersController.Login(new UserLoginDto("test_bot", "pass12345"));
        var loginOk = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loggedUser = Assert.IsType<UserResponseDto>(loginOk.Value);

        Assert.Equal(registeredUser.Id, loggedUser.Id);
        Assert.False(string.IsNullOrWhiteSpace(loggedUser.Token));

        // 3. Ошибка авторизации при неверном пароле
        var badLoginResult = await usersController.Login(new UserLoginDto("test_bot", "wrong_pass"));
        Assert.IsType<UnauthorizedObjectResult>(badLoginResult.Result);
    }

    [Fact]
    public async Task Users_Authorization_ProtectsGetMeEndpoint()
    {
        var (context, _, _) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);

        // Создаем пользователя в БД
        var regResult = await usersController.Register(new UserRegisterDto("authorized_user", "mypass"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        // 1. Неавторизованный запрос (без токена/клеймов)
        usersController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var unauthorizedResult = await usersController.GetMe();
        Assert.IsType<UnauthorizedObjectResult>(unauthorizedResult.Result);

        // 2. Авторизованный запрос с клеймами пользователя
        SetUserContext(usersController, user.Id, user.Username);
        var authorizedResult = await usersController.GetMe();
        var okResult = Assert.IsType<OkObjectResult>(authorizedResult.Result);
        var profile = Assert.IsType<UserResponseDto>(okResult.Value);

        Assert.Equal(user.Id, profile.Id);
        Assert.Equal("authorized_user", profile.Username);
    }

    [Fact]
    public async Task Users_RegistrationAndWatering_FlowWorks()
    {
        var (context, _, mockClients) = CreateEnvironment();
        var usersController = new UsersController(context, _configuration);
        var mockHubContext = new Mock<IHubContext<TelemetryHub>>();
        mockHubContext.Setup(h => h.Clients.All).Returns(mockClients.Object);
        var plantsController = new PlantsController(context, mockHubContext.Object);

        // 1. Регистрация
        var regResult = await usersController.Register(new UserRegisterDto("gardener_bot", "secret123"));
        var createdResult = Assert.IsType<CreatedAtActionResult>(regResult.Result);
        var user = Assert.IsType<UserResponseDto>(createdResult.Value);

        // 2. Привязка растения к пользователю
        SetUserContext(plantsController, user.Id, user.Username);
        var plantResult = await plantsController.Create(new PlantCreateDto(
            Name: "Монстера", Species: "Monstera deliciosa"));
        var plantCreated = Assert.IsType<CreatedAtActionResult>(plantResult.Result);
        var plant = Assert.IsType<PlantResponseDto>(plantCreated.Value);

        Assert.Equal(user.Id, plant.UserId);

        // 3. Создание инцидента засухи
        context.Alerts.Add(new Alert
        {
            PlantId = plant.Id,
            Type = AlertType.LowMoisture,
            Message = "Терміново потрібен полив",
            IsResolved = false
        });
        await context.SaveChangesAsync();

        // 4. Полив растения
        var waterResult = await plantsController.WaterPlant(plant.Id);
        Assert.IsType<OkObjectResult>(waterResult);

        // Проверка закрытия инцидента
        var alert = await context.Alerts.FirstAsync(a => a.PlantId == plant.Id);
        Assert.True(alert.IsResolved);

        // Проверка рассылки события ReceiveWatering в SignalR
        mockClients.Verify(
            c => c.SendCoreAsync("ReceiveWatering", It.IsAny<object[]>(), default),
            Times.Once);
    }
}