namespace CarLease.Api.Services;

public interface IOtpEmailSender
{
    Task SendAsync(string recipient, string otp, CancellationToken cancellationToken);
}
