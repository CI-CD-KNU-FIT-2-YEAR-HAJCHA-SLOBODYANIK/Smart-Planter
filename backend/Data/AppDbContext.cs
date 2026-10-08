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

        // Обязательная связь один-ко-многим: Пользователь -> Растения
        modelBuilder.Entity<Plant>()
            .HasOne(p => p.User)
            .WithMany(u => u.Plants)
            .HasForeignKey(p => p.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Уникальный индекс для поиска растения по Device API-Key
        modelBuilder.Entity<Plant>()
            .HasIndex(p => p.ApiKey)
            .IsUnique();

        // Составной индекс для выборки истории измерений по растению и дате
        modelBuilder.Entity<Telemetry>()
            .HasIndex(t => new { t.PlantId, t.Timestamp });

        // Составной индекс для выборки активных инцидентов
        modelBuilder.Entity<Alert>()
            .HasIndex(a => new { a.PlantId, a.IsResolved });
    }
}