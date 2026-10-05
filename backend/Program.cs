using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SmartPlanter.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();  // не забудь запустить docker compose up -d db!!!
}

app.MapOpenApi();
app.MapScalarApiReference();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run(); // Заходить на http://localhost:5000/scalar/v1