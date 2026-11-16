namespace CarLease.Api.Models;

public sealed class SdlcDemoRecord
{
    public long Id { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
