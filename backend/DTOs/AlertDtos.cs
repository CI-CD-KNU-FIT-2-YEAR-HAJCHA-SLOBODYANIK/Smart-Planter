namespace SmartPlanter.Api.DTOs;

using SmartPlanter.Api.Models;

public record AlertResponseDto(
    int Id,
    int PlantId,
    string PlantName,
    AlertType Type,
    string Message,
    DateTime CreatedAt,
    bool IsResolved,
    DateTime? ResolvedAt
);