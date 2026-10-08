namespace SmartPlanter.Api.Models;

public class Telemetry
{
    public int Id { get; set; }

    public int PlantId { get; set; }
    public Plant? Plant { get; set; }

    public float Moisture { get; set; }     // %
    public float Temperature { get; set; }  // °C
    public float Light { get; set; }        // Lux

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}