using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SmartPlanter.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Регистрация контроллеров и генератора OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 2. Регистрация PostgreSQL через Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 3. Настройка CORS для будущей интеграции с фронтендом
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 4. Автоматическое создание таблиц в БД при запуске приложения (для этапа разработки)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// 5. Конвейер обработки HTTP-запросов
app.MapOpenApi();
app.MapScalarApiReference();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();