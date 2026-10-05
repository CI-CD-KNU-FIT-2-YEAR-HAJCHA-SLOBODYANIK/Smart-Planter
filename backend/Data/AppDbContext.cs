using Microsoft.EntityFrameworkCore;
using SmartPlanter.Api.Models;

namespace SmartPlanter.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<Telemetry> Telemetries => Set<Telemetry>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Индекс для ускорения выборки истории по конкретному растению за период
        modelBuilder.Entity<Telemetry>()
            .HasIndex(t => new { t.PlantId, t.Timestamp });

        // Индекс для выборки активных алертов
        modelBuilder.Entity<Alert>()
            .HasIndex(a => new { a.PlantId, a.IsResolved });
    }
}