using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartPlanter.Api.Data;
using SmartPlanter.Api.DTOs;
using SmartPlanter.Api.Hubs;
using SmartPlanter.Api.Models;

namespace SmartPlanter.Api.Controllers;

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

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PlantResponseDto>>> GetAll([FromQuery] int? userId = null)
    {
        var query = _context.Plants.AsQueryable();

        var effectiveUserId = userId ?? GetCurrentUserId();
        if (effectiveUserId.HasValue)
        {
            query = query.Where(p => p.UserId == effectiveUserId.Value);
        }

        var plants = await query
            .Select(p => new PlantResponseDto(
                p.Id,
                p.Name,
                p.Species,
                p.MinMoisture,
                p.MaxMoisture,
                p.MinTemp,
                p.MaxTemp,
                p.MinLight,
                p.CreatedAt,
                p.UserId))
            .ToListAsync();

        return Ok(plants);
    }

    [HttpPost]
    public async Task<ActionResult<PlantResponseDto>> Create([FromBody] PlantCreateDto dto)
    {
        var targetUserId = dto.UserId ?? GetCurrentUserId();

        if (targetUserId.HasValue)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == targetUserId.Value);
            if (!userExists)
            {
                return BadRequest($"Користувача з ID {targetUserId.Value} не існує.");
            }
        }

        var plant = new Plant
        {
            Name = dto.Name,
            Species = dto.Species,
            MinMoisture = dto.MinMoisture,
            MaxMoisture = dto.MaxMoisture,
            MinTemp = dto.MinTemp,
            MaxTemp = dto.MaxTemp,
            MinLight = dto.MinLight,
            UserId = targetUserId
        };

        _context.Plants.Add(plant);
        await _context.SaveChangesAsync();

        var response = new PlantResponseDto(
            plant.Id,
            plant.Name,
            plant.Species,
            plant.MinMoisture,
            plant.MaxMoisture,
            plant.MinTemp,
            plant.MaxTemp,
            plant.MinLight,
            plant.CreatedAt,
            plant.UserId);

        return CreatedAtAction(nameof(GetAll), new { id = plant.Id }, response);
    }

    [HttpPost("{id:int}/water")]
    public async Task<IActionResult> WaterPlant(int id)
    {
        var plant = await _context.Plants.FindAsync(id);
        if (plant == null)
        {
            return NotFound($"Рослину з ID {id} не знайдено.");
        }

        // Закрываем активные оповещения о недостатке влаги
        var lowMoistureAlerts = await _context.Alerts
            .Where(a => a.PlantId == id && a.Type == AlertType.LowMoisture && !a.IsResolved)
            .ToListAsync();

        foreach (var alert in lowMoistureAlerts)
        {
            alert.IsResolved = true;
            alert.ResolvedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Оповещение WebSocket клиентов о факте полива
        await _hubContext.Clients.All.SendAsync("ReceiveWatering", new { plantId = id, timestamp = DateTime.UtcNow });

        return Ok(new { message = $"Полив для рослини '{plant.Name}' успішно зафіксовано." });
    }
}