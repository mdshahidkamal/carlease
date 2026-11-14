using System.ComponentModel.DataAnnotations;

namespace CarLease.Api.DTOs;

public sealed record CreateSdlcDemoRecordRequest(
    [property: Required, StringLength(500)] string Message);
