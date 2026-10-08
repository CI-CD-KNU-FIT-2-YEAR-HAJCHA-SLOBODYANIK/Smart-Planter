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

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    [HttpPost]
    public async Task<ActionResult<TelemetryResponseDto>> PostTelemetry([FromBody] TelemetryCreateDto dto)
    {
        // Проверка аутентификации датчика через заголовок X-Device-Key
        if (!Request.Headers.TryGetValue("X-Device-Key", out var apiKeyValues) ||
            string.IsNullOrWhiteSpace(apiKeyValues.FirstOrDefault()))
        {
            return Unauthorized("API-ключ пристрою відсутній у заголовку 'X-Device-Key'.");
        }

        var deviceKey = apiKeyValues.First()!.Trim();

        var plant = await _context.Plants
            .FirstOrDefaultAsync(p => p.ApiKey == deviceKey);

        if (plant == null)
        {
            return Unauthorized("Недійсний API-ключ пристрою.");
        }

        // Если устройство не передало время, устанавливаем текущий UTC
        var timestamp = dto.Timestamp.HasValue && dto.Timestamp.Value != default
            ? dto.Timestamp.Value.ToUniversalTime()
            : DateTime.UtcNow;

        var entry = new Telemetry
        {
            PlantId = plant.Id,
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

        // 1. Контроль влажности
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

        // 2. Контроль температуры
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

        var userTarget = plant.UserId.ToString();

        // Адресная трансляция метрик только владельцу растения
        await _hubContext.Clients.User(userTarget).SendAsync("ReceiveTelemetry", new
        {
            entry.Id,
            entry.PlantId,
            entry.Moisture,
            entry.Temperature,
            entry.Light,
            entry.Timestamp
        });

        // Адресная отправка новых оповещений только владельцу
        foreach (var alert in newAlerts)
        {
            await _hubContext.Clients.User(userTarget).SendAsync("ReceiveAlert", new AlertResponseDto(
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

    [Authorize]
    [HttpGet("{plantId:int}/history")]
    public async Task<ActionResult<IEnumerable<TelemetryResponseDto>>> GetHistory(
        int plantId,
        [FromQuery] int limit = 50,
        [FromQuery] int? days = null,
        [FromQuery] int? hours = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var userId = GetCurrentUserId();

        // Проверяем принадлежность запрашиваемого растения текущему пользователю
        var plantExists = await _context.Plants
            .AnyAsync(p => p.Id == plantId && p.UserId == userId);

        if (!plantExists)
        {
            return NotFound($"Рослину з ID {plantId} не знайдено.");
        }

        var query = _context.Telemetries.Where(t => t.PlantId == plantId);

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

        if (from.HasValue)
        {
            query = query.Where(t => t.Timestamp >= from.Value.ToUniversalTime());
        }

        if (to.HasValue)
        {
            query = query.Where(t => t.Timestamp <= to.Value.ToUniversalTime());
        }

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