using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartPlanter.Api.Data;
using SmartPlanter.Api.DTOs;
using SmartPlanter.Api.Models;

namespace SmartPlanter.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public UsersController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    private string GenerateJwtToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "SmartPlanter_Secret_Key_For_Jwt_Token_Auth_2026_Secure_Key!";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "SmartPlanterApi";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "SmartPlanterApp";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username)
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserResponseDto>> Register([FromBody] UserRegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest("Ім'я користувача та пароль не можуть бути порожніми.");
        }

        var exists = await _context.Users.AnyAsync(u => u.Username == dto.Username.Trim());
        if (exists)
        {
            return Conflict($"Користувач із нікнеймом '{dto.Username}' вже зареєстрований.");
        }

        var user = new User
        {
            Username = dto.Username.Trim(),
            PasswordHash = HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user);

        return CreatedAtAction(
            nameof(GetById),
            new { id = user.Id },
            new UserResponseDto(user.Id, user.Username, user.CreatedAt, token));
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserResponseDto>> Login([FromBody] UserLoginDto dto)
    {
        var user = await _context.Users
            .Include(u => u.Plants)
            .FirstOrDefaultAsync(u => u.Username == dto.Username.Trim());

        if (user == null || user.PasswordHash != HashPassword(dto.Password))
        {
            return Unauthorized("Невірне ім'я користувача або пароль.");
        }

        var token = GenerateJwtToken(user);

        var plantsDto = user.Plants.Select(p => new PlantResponseDto(
            p.Id, p.Name, p.Species, p.ApiKey, p.MinMoisture, p.MaxMoisture,
            p.MinTemp, p.MaxTemp, p.MinLight, p.CreatedAt));

        return Ok(new UserResponseDto(user.Id, user.Username, user.CreatedAt, token, plantsDto));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponseDto>> GetMe()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Користувач не авторизований.");
        }

        var user = await _context.Users
            .Include(u => u.Plants)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return NotFound("Користувача не знайдено.");
        }

        var plantsDto = user.Plants.Select(p => new PlantResponseDto(
            p.Id, p.Name, p.Species, p.ApiKey, p.MinMoisture, p.MaxMoisture,
            p.MinTemp, p.MaxTemp, p.MinLight, p.CreatedAt));

        return Ok(new UserResponseDto(user.Id, user.Username, user.CreatedAt, null, plantsDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserResponseDto>> GetById(int id)
    {
        var user = await _context.Users
            .Include(u => u.Plants)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound($"Користувача з ID {id} не знайдено.");
        }

        var plantsDto = user.Plants.Select(p => new PlantResponseDto(
            p.Id, p.Name, p.Species, p.ApiKey, p.MinMoisture, p.MaxMoisture,
            p.MinTemp, p.MaxTemp, p.MinLight, p.CreatedAt));

        return Ok(new UserResponseDto(user.Id, user.Username, user.CreatedAt, null, plantsDto));
    }
}