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

    [HttpPost]
    public async Task<ActionResult<TelemetryResponseDto>> PostTelemetry([FromBody] TelemetryCreateDto dto)
    {
        var plant = await _context.Plants.FindAsync(dto.PlantId);
        if (plant == null)
        {
            return NotFound($"Рослину з ID {dto.PlantId} не знайдено.");
        }

        // Если метка времени не передана, устанавливается текущее системное время в UTC
        var timestamp = dto.Timestamp.HasValue && dto.Timestamp.Value != default
            ? dto.Timestamp.Value.ToUniversalTime()
            : DateTime.UtcNow;

        var entry = new Telemetry
        {
            PlantId = dto.PlantId,
            Moisture = dto.Moisture,
            Temperature = dto.Temperature,
            Light = dto.Light,
            Timestamp = timestamp
        };

        _context.Telemetries.Add(entry);

        var activeAlertTypes = await _context.Alerts
            .Where(a => a.PlantId == plant.Id && !a.IsResolved)
            .Select(a => a.Type)
            .ToListAsync();

        var newAlerts = new List<Alert>();

        // 1. Контроль влажности почвы
        if (entry.Moisture < plant.MinMoisture && !activeAlertTypes.Contains(AlertType.LowMoisture))
        {
            newAlerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.LowMoisture,
                Message = $"Низька вологість: {entry.Moisture:F1}% (мінімум: {plant.MinMoisture:F1}%)",
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (entry.Moisture > plant.MaxMoisture && !activeAlertTypes.Contains(AlertType.HighMoisture))
        {
            newAlerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.HighMoisture,
                Message = $"Перезволоження ґрунту: {entry.Moisture:F1}% (максимум: {plant.MaxMoisture:F1}%)",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 2. Контроль температуры воздуха
        if (entry.Temperature < plant.MinTemp && !activeAlertTypes.Contains(AlertType.LowTemperature))
        {
            newAlerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.LowTemperature,
                Message = $"Низька температура: {entry.Temperature:F1}°C (мінімум: {plant.MinTemp:F1}°C)",
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (entry.Temperature > plant.MaxTemp && !activeAlertTypes.Contains(AlertType.HighTemperature))
        {
            newAlerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.HighTemperature,
                Message = $"Перевищення температури: {entry.Temperature:F1}°C (максимум: {plant.MaxTemp:F1}°C)",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 3. Контроль освещенности
        if (entry.Light < plant.MinLight && !activeAlertTypes.Contains(AlertType.LowLight))
        {
            newAlerts.Add(new Alert
            {
                PlantId = plant.Id,
                Type = AlertType.LowLight,
                Message = $"Недостатньо світла: {entry.Light:F0} Lux (мінімум: {plant.MinLight:F0} Lux)",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (newAlerts.Count > 0)
        {
            _context.Alerts.AddRange(newAlerts);
        }

        await _context.SaveChangesAsync();

        // Передача измерений клиентам через SignalR
        await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
        {
            entry.Id,
            entry.PlantId,
            entry.Moisture,
            entry.Temperature,
            entry.Light,
            entry.Timestamp
        });

        // Мгновенная рассылка сформированных оповещений
        foreach (var alert in newAlerts)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveAlert", new AlertResponseDto(
                alert.Id,
                alert.PlantId,
                plant.Name,
                alert.Type,
                alert.Message,
                alert.CreatedAt,
                alert.IsResolved,
                alert.ResolvedAt
            ));
        }

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
        [FromQuery] int limit = 50,
        [FromQuery] int? days = null,
        [FromQuery] int? hours = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _context.Telemetries.Where(t => t.PlantId == plantId);

        // Ограничение по временному срезу назад от текущего момента
        if (days.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days.Value);
            query = query.Where(t => t.Timestamp >= cutoff);
        }
        else if (hours.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddHours(-hours.Value);
            query = query.Where(t => t.Timestamp >= cutoff);
        }

        // Фильтрация по заданным границам дат
        if (from.HasValue)
        {
            query = query.Where(t => t.Timestamp >= from.Value.ToUniversalTime());
        }

        if (to.HasValue)
        {
            query = query.Where(t => t.Timestamp <= to.Value.ToUniversalTime());
        }

        // Сортировка: от новых записей к старым
        var data = await query
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