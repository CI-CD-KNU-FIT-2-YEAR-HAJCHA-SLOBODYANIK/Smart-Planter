using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPlanter.Api.Data;
using SmartPlanter.Api.DTOs;
using SmartPlanter.Api.Models;

namespace SmartPlanter.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly AppDbContext _context;

    public TelemetryController(AppDbContext context)
    {
        _context = context;
    }

    /*
     * Точка входа для получения пакета телеметрии от устройства.
     * POST /api/v1/telemetry
     */
    [HttpPost]
    public async Task<ActionResult<TelemetryResponseDto>> PostTelemetry([FromBody] TelemetryCreateDto dto)
    {
        var plant = await _context.Plants.FindAsync(dto.PlantId);
        if (plant == null)
        {
            return NotFound($"Растение с ID {dto.PlantId} не найдено.");
        }

        var entry = new Telemetry
        {
            PlantId = dto.PlantId,
            Moisture = dto.Moisture,
            Temperature = dto.Temperature,
            Light = dto.Light,
            Timestamp = DateTime.UtcNow
        };

        _context.Telemetries.Add(entry);

        // Получаем типы текущих незакрытых тревог для этого растения, чтобы не дублировать их
        var activeAlertTypes = await _context.Alerts
            .Where(a => a.PlantId == plant.Id && !a.IsResolved)
            .Select(a => a.Type)
            .ToListAsync();

        // 1. Контроль влажности
        if (entry.Moisture < plant.MinMoisture && !activeAlertTypes.Contains(AlertType.LowMoisture))
        {
            _context.Alerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.LowMoisture,
                Message = $"Низкая влажность: {entry.Moisture:F1}% (минимум: {plant.MinMoisture:F1}%)",
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (entry.Moisture > plant.MaxMoisture && !activeAlertTypes.Contains(AlertType.HighMoisture))
        {
            _context.Alerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.HighMoisture,
                Message = $"Перелив почвы: {entry.Moisture:F1}% (максимум: {plant.MaxMoisture:F1}%)",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 2. Контроль температуры
        if (entry.Temperature < plant.MinTemp && !activeAlertTypes.Contains(AlertType.LowTemperature))
        {
            _context.Alerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.LowTemperature,
                Message = $"Низкая температура: {entry.Temperature:F1}°C (минимум: {plant.MinTemp:F1}°C)",
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (entry.Temperature > plant.MaxTemp && !activeAlertTypes.Contains(AlertType.HighTemperature))
        {
            _context.Alerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.HighTemperature,
                Message = $"Превышение температуры: {entry.Temperature:F1}°C (максимум: {plant.MaxTemp:F1}°C)",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 3. Контроль освещенности
        if (entry.Light < plant.MinLight && !activeAlertTypes.Contains(AlertType.LowLight))
        {
            _context.Alerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.LowLight,
                Message = $"Недостаток света: {entry.Light:F0} Lux (минимум: {plant.MinLight:F0} Lux)",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new TelemetryResponseDto(
            entry.Id,
            entry.PlantId,
            entry.Moisture,
            entry.Temperature,
            entry.Light,
            entry.Timestamp));
    }

    [HttpGet("{plantId:int}/history")]
    public async Task<ActionResult<IEnumerable<TelemetryResponseDto>>> GetHistory(
        int plantId,
        [FromQuery] int limit = 50)
    {
        var data = await _context.Telemetries
            .Where(t => t.PlantId == plantId)
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .Select(t => new TelemetryResponseDto(
                t.Id,
                t.PlantId,
                t.Moisture,
                t.Temperature,
                t.Light,
                t.Timestamp))
            .ToListAsync();

        return Ok(data);
    }
}