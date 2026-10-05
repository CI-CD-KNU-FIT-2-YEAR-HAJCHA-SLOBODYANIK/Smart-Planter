namespace SmartPlanter.Api.DTOs;

// Модель для приема пакета с датчиков
public record TelemetryCreateDto(
    int PlantId,
    float Moisture,
    float Temperature,
    float Light
);

// Модель для отдачи точки графика клиенту
public record TelemetryResponseDto(
    int Id,
    int PlantId,
    float Moisture,
    float Temperature,
    float Light,
    DateTime Timestamp
);