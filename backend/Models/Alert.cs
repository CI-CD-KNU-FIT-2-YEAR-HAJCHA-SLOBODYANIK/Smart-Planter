namespace SmartPlanter.Api.Models;

public enum AlertType
{
    LowMoisture,     // Требуется полив
    HighMoisture,    // Перелив
    LowTemperature,  // Переохлаждение
    HighTemperature, // Перегрев
    LowLight         // Недоосвещенность
}

public class Alert
{
    public int Id { get; set; }

    public int PlantId { get; set; }
    public Plant? Plant { get; set; }

    public AlertType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }
}