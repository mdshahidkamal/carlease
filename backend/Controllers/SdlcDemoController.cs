using CarLease.Api.Data;
using CarLease.Api.DTOs;
using CarLease.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Controllers;

[ApiController]
[Route("api/sdlc-demo")]
public sealed class SdlcDemoController(
    ApplicationDbContext db,
    IHostEnvironment hostEnvironment) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SdlcDemoRecord>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SdlcDemoRecord>>> Get(
        CancellationToken cancellationToken)
    {
        var records = await db.SdlcDemoRecords
            .AsNoTracking()
            .OrderBy(record => record.Id)
            .ToListAsync(cancellationToken);

        return Ok(records);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SdlcDemoRecord), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SdlcDemoRecord>> Create(
        CreateSdlcDemoRecordRequest request,
        CancellationToken cancellationToken)
    {
        var record = new SdlcDemoRecord
        {
            Message = request.Message.Trim(),
            Environment = hostEnvironment.EnvironmentName,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.SdlcDemoRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), value: record);
    }
}
