namespace SmartPlanter.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Растения, принадлежащие пользователю
    public List<Plant> Plants { get; set; } = new();
}