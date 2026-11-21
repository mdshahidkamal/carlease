using CarLease.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(vehicle => vehicle.IsActive)
            .OrderBy(vehicle => vehicle.VehicleName)
            .Select(vehicle => new { vehicle.Id, vehicle.VehicleName, vehicle.PassengerCapacity, vehicle.LuggageCapacity })
            .ToListAsync(cancellationToken);
        return Ok(vehicles);
    }
}
