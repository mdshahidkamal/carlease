namespace CarLease.Api.Models;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string VehicleName { get; set; } = string.Empty;
    public int PassengerCapacity { get; set; }
    public int LuggageCapacity { get; set; }
    public bool IsActive { get; set; } = true;
}
