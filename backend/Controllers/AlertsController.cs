using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPlanter.Api.Data;
using SmartPlanter.Api.DTOs;

namespace SmartPlanter.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AlertsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<AlertResponseDto>>> GetActiveAlerts([FromQuery] int? plantId = null)
    {
        var query = _context.Alerts
            .Include(a => a.Plant)
            .Where(a => !a.IsResolved);

        if (plantId.HasValue)
        {
            query = query.Where(a => a.PlantId == plantId.Value);
        }

        var alerts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AlertResponseDto(
                a.Id,
                a.PlantId,
                a.Plant != null ? a.Plant.Name : string.Empty,
                a.Type,
                a.Message,
                a.CreatedAt,
                a.IsResolved,
                a.ResolvedAt
            ))
            .ToListAsync();

        return Ok(alerts);
    }

    [HttpPut("{id:int}/resolve")]
    public async Task<IActionResult> ResolveAlert(int id)
    {
        var alert = await _context.Alerts.FindAsync(id);
        if (alert == null)
        {
            return NotFound($"Інцидент з ID {id} не знайдено.");
        }

        if (alert.IsResolved)
        {
            return BadRequest("Інцидент вже був закритий раніше.");
        }

        alert.IsResolved = true;
        alert.ResolvedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }
}