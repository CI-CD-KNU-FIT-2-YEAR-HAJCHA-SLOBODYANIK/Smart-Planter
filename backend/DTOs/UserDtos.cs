namespace SmartPlanter.Api.DTOs;

public record UserRegisterDto(
    string Username,
    string Password);

public record UserLoginDto(
    string Username,
    string Password);

public record UserResponseDto(
    int Id,
    string Username,
    DateTime CreatedAt,
    string? Token = null,
    IEnumerable<PlantResponseDto>? Plants = null);