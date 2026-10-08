using Microsoft.EntityFrameworkCore;
using SmartPlanter.Api.Models;

namespace SmartPlanter.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<Telemetry> Telemetries => Set<Telemetry>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Уникальный логин пользователя
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // Связь один-ко-многим: Пользователь -> Растения
        modelBuilder.Entity<Plant>()
            .HasOne(p => p.User)
            .WithMany(u => u.Plants)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Составной индекс для выборки истории измерений
        modelBuilder.Entity<Telemetry>()
            .HasIndex(t => new { t.PlantId, t.Timestamp });

        // Составной индекс для активных инцидентов
        modelBuilder.Entity<Alert>()
            .HasIndex(a => new { a.PlantId, a.IsResolved });
    }
}