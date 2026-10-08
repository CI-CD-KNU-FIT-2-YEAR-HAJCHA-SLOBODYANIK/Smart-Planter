using SmartPlanter.Api.Models;

namespace SmartPlanter.Api.DTOs;

public record AlertResponseDto(
    int Id,
    int PlantId,
    string PlantName,
    AlertType Type,
    string Message,
    DateTime CreatedAt,
    bool IsResolved,
    DateTime? ResolvedAt);