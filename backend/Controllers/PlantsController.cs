using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPlanter.Api.Data;
using SmartPlanter.Api.DTOs;
using SmartPlanter.Api.Models;

namespace SmartPlanter.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PlantsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PlantsController(AppDbContext context)
    {
        _context = context;
    }

    /*
     * Получение полного списка зарегистрированных растений.
     * GET /api/v1/plants
     */
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PlantResponseDto>>> GetAll()
    {
        var plants = await _context.Plants
            .Select(p => new PlantResponseDto(
                p.Id,
                p.Name,
                p.Species,
                p.MinMoisture,
                p.MaxMoisture,
                p.MinTemp,
                p.MaxTemp,
                p.MinLight,
                p.CreatedAt))
            .ToListAsync();

        return Ok(plants);
    }
    /*
     * Создание нового растения и фиксация его пороговых значений жизнедеятельности.
     * POST /api/v1/plants
     */
    [HttpPost]
    public async Task<ActionResult<PlantResponseDto>> Create([FromBody] PlantCreateDto dto)
    {
        var plant = new Plant
        {
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
            plant.MinMoisture,
            plant.MaxMoisture,
            plant.MinTemp,
            plant.MaxTemp,
            plant.MinLight,
            plant.CreatedAt);

        return CreatedAtAction(nameof(GetAll), new { id = plant.Id }, response);
    }

    // POST /api/v1/plants/{id}/water
    [HttpPost("{id:int}/water")]
    public async Task<IActionResult> WaterPlant(int id)
    {
        var plant = await _context.Plants.FindAsync(id);
        if (plant == null)
        {
            return NotFound($"Рослину з ID {id} не знайдено.");
        }

        // Закрываем активные алерты о засухе для этого растения
        var lowMoistureAlerts = await _context.Alerts
            .Where(a => a.PlantId == id && a.Type == AlertType.LowMoisture && !a.IsResolved)
            .ToListAsync();

        foreach (var alert in lowMoistureAlerts)
        {
            alert.IsResolved = true;
            alert.ResolvedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = $"Полив для рослини '{plant.Name}' зафіксовано." });
    }
}