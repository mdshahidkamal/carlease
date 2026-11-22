using System.Security.Cryptography;
using System.Text;
using CarLease.Api.Data;
using CarLease.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Controllers;

[ApiController]
[Route("api/admin/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminAuthController(ApplicationDbContext db, IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    public const string CookieName = "carlease_admin";
    private const int SessionDays = 8;

    [HttpPost("login")]
    public async Task<IActionResult> Login(AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var configuredEmail = configuration["Admin:Email"]?.Trim();
        var configuredPassword = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(configuredEmail) || string.IsNullOrWhiteSpace(configuredPassword))
            return StatusCode(503, new { message = "Admin access is not configured on the server." });

        var emailHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Email.Trim().ToLowerInvariant()));
        var expectedEmailHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredEmail.ToLowerInvariant()));
        var passwordHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password));
        var expectedPasswordHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredPassword));
        if (!CryptographicOperations.FixedTimeEquals(emailHash, expectedEmailHash) ||
            !CryptographicOperations.FixedTimeEquals(passwordHash, expectedPasswordHash))
            return Unauthorized(new { message = "Invalid administrator credentials." });

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = DateTime.UtcNow;
        db.AdminSessions.Add(new AdminSession
        {
            TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token)),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(SessionDays)
        });
        await db.SaveChangesAsync(cancellationToken);
        Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Lax,
            Path = "/", MaxAge = TimeSpan.FromDays(SessionDays)
        });
        return Ok(new { email = configuredEmail, expiresAtUtc = now.AddDays(SessionDays) });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(cancellationToken);
        return session is null ? Unauthorized() : Ok(new { email = configuration["Admin:Email"] });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(cancellationToken);
        if (session is not null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        Response.Cookies.Delete(CookieName, new CookieOptions { HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Lax, Path = "/" });
        return Ok(new { message = "Administrator signed out." });
    }

    internal async Task<AdminSession?> FindSessionAsync(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var now = DateTime.UtcNow;
        return await db.AdminSessions.SingleOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken);
    }
}

public sealed record AdminLoginRequest(string Email, string Password);
