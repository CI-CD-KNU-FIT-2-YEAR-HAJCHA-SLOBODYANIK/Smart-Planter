namespace SmartPlanter.Api.DTOs;

public record TelemetryCreateDto(
    float Moisture,
    float Temperature,
    float Light,
    DateTime? Timestamp = null);

public record TelemetryResponseDto(
    int Id,
    int PlantId,
    float Moisture,
    float Temperature,
    float Light,
    DateTime Timestamp);