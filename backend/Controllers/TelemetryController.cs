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
public class TelemetryController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHubContext<TelemetryHub> _hubContext;

    public TelemetryController(AppDbContext context, IHubContext<TelemetryHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    /*
     * Точка входу для отримання пакета телеметрії від пристрою.
     * POST /api/v1/telemetry
     */
    [HttpPost]
    public async Task<ActionResult<TelemetryResponseDto>> PostTelemetry([FromBody] TelemetryCreateDto dto)
    {
        var plant = await _context.Plants.FindAsync(dto.PlantId);
        if (plant == null)
        {
            return NotFound($"Рослину з ID {dto.PlantId} не знайдено.");
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

        // Отримуємо типи активних незакритих тривог для цієї рослини, щоб уникнути дублювання
        var activeAlertTypes = await _context.Alerts
            .Where(a => a.PlantId == plant.Id && !a.IsResolved)
            .Select(a => a.Type)
            .ToListAsync();

        // 1. Контроль вологості
        if (entry.Moisture < plant.MinMoisture && !activeAlertTypes.Contains(AlertType.LowMoisture))
        {
            _context.Alerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.LowMoisture,
                Message = $"Низька вологість: {entry.Moisture:F1}% (мінімум: {plant.MinMoisture:F1}%)",
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (entry.Moisture > plant.MaxMoisture && !activeAlertTypes.Contains(AlertType.HighMoisture))
        {
            _context.Alerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.HighMoisture,
                Message = $"Перезволоження ґрунту: {entry.Moisture:F1}% (максимум: {plant.MaxMoisture:F1}%)",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 2. Контроль температури
        if (entry.Temperature < plant.MinTemp && !activeAlertTypes.Contains(AlertType.LowTemperature))
        {
            _context.Alerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.LowTemperature,
                Message = $"Низька температура: {entry.Temperature:F1}°C (мінімум: {plant.MinTemp:F1}°C)",
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (entry.Temperature > plant.MaxTemp && !activeAlertTypes.Contains(AlertType.HighTemperature))
        {
            _context.Alerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.HighTemperature,
                Message = $"Перевищення температури: {entry.Temperature:F1}°C (максимум: {plant.MaxTemp:F1}°C)",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 3. Контроль освітленості
        if (entry.Light < plant.MinLight && !activeAlertTypes.Contains(AlertType.LowLight))
        {
            _context.Alerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.LowLight,
                Message = $"Недостатньо світла: {entry.Light:F0} Lux (мінімум: {plant.MinLight:F0} Lux)",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // Трансляція події всім підключеним клієнтам через WebSocket
        await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
        {
            entry.Id,
            entry.PlantId,
            entry.Moisture,
            entry.Temperature,
            entry.Light,
            entry.Timestamp
        });

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