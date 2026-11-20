using System.Security.Cryptography;
using System.Text;

namespace CarLease.Api.Services;

public static class AuthTokenHash
{
    public static byte[] HashOtp(string email, string otp, string pepper) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), Encoding.UTF8.GetBytes($"{email}:{otp}"));

    public static byte[] HashSessionToken(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
