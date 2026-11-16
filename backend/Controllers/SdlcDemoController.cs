using CarLease.Api.Data;
using CarLease.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Controllers;

[ApiController]
[Route("api/sdlc-demo")]
public sealed class SdlcDemoController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SdlcDemoRecord>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SdlcDemoRecord>>> Get(CancellationToken cancellationToken)
    {
        var records = await db.SdlcDemoRecords
            .AsNoTracking()
            .OrderBy(record => record.Id)
            .ToListAsync(cancellationToken);

        return Ok(records);
    }
}
