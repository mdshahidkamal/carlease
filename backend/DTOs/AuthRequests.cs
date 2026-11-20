using System.ComponentModel.DataAnnotations;

namespace CarLease.Api.DTOs;

public class RegisterRequest
{
    [Required, StringLength(120, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(160)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(30, MinimumLength = 5)] public string Phone { get; set; } = string.Empty;
    public bool ConsentAccepted { get; set; }
}

public class EmailRequest
{
    [Required, EmailAddress, StringLength(160)] public string Email { get; set; } = string.Empty;
}

public class VerifyOtpRequest : EmailRequest
{
    [Required, RegularExpression("^[0-9]{6}$")] public string Code { get; set; } = string.Empty;
}

public record AuthMessageResponse(string Message);
public record AuthCustomerResponse(Guid Id, string Name, string Email, string Phone);
