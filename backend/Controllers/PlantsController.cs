using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartPlanter.Api.Data;
using SmartPlanter.Api.DTOs;
using SmartPlanter.Api.Hubs;
using SmartPlanter.Api.Models;

namespace SmartPlanter.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class PlantsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHubContext<TelemetryHub> _hubContext;

    public PlantsController(AppDbContext context, IHubContext<TelemetryHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PlantResponseDto>>> GetAll()
    {
        var userId = GetCurrentUserId();

        var plants = await _context.Plants
            .Where(p => p.UserId == userId)
            .Select(p => new PlantResponseDto(
                p.Id,
                p.Name,
                p.Species,
                p.ApiKey,
                p.MinMoisture,
                p.MaxMoisture,
                p.MinTemp,
                p.MaxTemp,
                p.MinLight,
                p.CreatedAt))
            .ToListAsync();

        return Ok(plants);
    }

    [HttpPost]
    public async Task<ActionResult<PlantResponseDto>> Create([FromBody] PlantCreateDto dto)
    {
        var userId = GetCurrentUserId();

        var plant = new Plant
        {
            UserId = userId,
            ApiKey = Guid.NewGuid().ToString("N"),
            Name = dto.Name,
            Species = dto.Species,
            MinMoisture = dto.MinMoisture,
            MaxMoisture = dto.MaxMoisture,
            MinTemp = dto.MinTemp,
            MaxTemp = dto.MaxTemp,
            MinLight = dto.MinLight
        };

        _context.Plants.Add(plant);
        await _context.SaveChangesAsync();

        var response = new PlantResponseDto(
            plant.Id,
            plant.Name,
            plant.Species,
            plant.ApiKey,
            plant.MinMoisture,
            plant.MaxMoisture,
            plant.MinTemp,
            plant.MaxTemp,
            plant.MinLight,
            plant.CreatedAt);

        return CreatedAtAction(nameof(GetAll), new { id = plant.Id }, response);
    }

    [HttpPost("{id:int}/water")]
    public async Task<IActionResult> WaterPlant(int id)
    {
        var userId = GetCurrentUserId();

        // Проверяем принадлежность растения текущему авторизованному пользователю
        var plant = await _context.Plants
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        if (plant == null)
        {
            return NotFound($"Рослину з ID {id} не знайдено.");
        }

        // Автоматически закрываем активные предупреждения о недостатке влаги
        var lowMoistureAlerts = await _context.Alerts
            .Where(a => a.PlantId == id && a.Type == AlertType.LowMoisture && !a.IsResolved)
            .ToListAsync();

        foreach (var alert in lowMoistureAlerts)
        {
            alert.IsResolved = true;
            alert.ResolvedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Адресная отправка события полива только владельцу растения
        await _hubContext.Clients.User(userId.ToString())
            .SendAsync("ReceiveWatering", new { plantId = id, timestamp = DateTime.UtcNow });

        return Ok(new { message = $"Полив для рослини '{plant.Name}' успішно зафіксовано." });
    }
}