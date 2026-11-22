namespace CarLease.Api.Models;

public class AdminSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public byte[] TokenHash { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
