namespace SmartPlanter.Api.DTOs;

public record PlantCreateDto(
    string Name = "no name",
    string Species = null!,
    float MinMoisture = 30.0f,
    float MaxMoisture = 70.0f,
    float MinTemp = 18.0f,
    float MaxTemp = 28.0f,
    float MinLight = 300.0f);

public record PlantResponseDto(
    int Id,
    string Name,
    string Species,
    string ApiKey,
    float MinMoisture,
    float MaxMoisture,
    float MinTemp,
    float MaxTemp,
    float MinLight,
    DateTime CreatedAt);