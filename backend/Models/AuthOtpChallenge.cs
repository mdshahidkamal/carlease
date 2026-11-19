namespace CarLease.Api.Models;

public class AuthOtpChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public byte[] OtpHash { get; set; } = [];
    public string? RegistrationName { get; set; }
    public string? RegistrationPhone { get; set; }
    public DateTime? ConsentAcceptedAtUtc { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
}
