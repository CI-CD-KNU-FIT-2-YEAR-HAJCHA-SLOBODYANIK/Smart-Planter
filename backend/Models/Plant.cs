namespace SmartPlanter.Api.Models;

public class Plant
{
    public int Id { get; set; }

    // Идентификатор владельца (опциональный для обратной совместимости)
    public int? UserId { get; set; }
    public User? User { get; set; }

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