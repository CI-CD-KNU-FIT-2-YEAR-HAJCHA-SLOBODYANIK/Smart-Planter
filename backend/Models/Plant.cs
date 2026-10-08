namespace SmartPlanter.Api.Models;

public class Plant
{
    public int Id { get; set; }

    // Обязательная привязка к владельцу
    public int UserId { get; set; }
    public User? User { get; set; }

    // Уникальный API-ключ устройства для аутентификации IoT-датчиков
    public string ApiKey { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;

    public float MinMoisture { get; set; } = 30.0f;  // % (0.0 - 100.0)
    public float MaxMoisture { get; set; } = 70.0f;  // % (0.0 - 100.0)
    public float MinTemp { get; set; } = 18.0f;      // °C
    public float MaxTemp { get; set; } = 28.0f;      // °C
    public float MinLight { get; set; } = 300.0f;    // Lux

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Telemetry> Telemetries { get; set; } = new();
    public List<Alert> Alerts { get; set; } = new();
}