using System.ComponentModel.DataAnnotations;

namespace CarLease.Api.DTOs;

public class CreateBookingRequest
{
    [Required, StringLength(500)] public string Pickup { get; set; } = string.Empty;
    public string? PickupPlaceId { get; set; }
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    [Required, StringLength(500)] public string Dropoff { get; set; } = string.Empty;
    public string? DropoffPlaceId { get; set; }
    public double? DropoffLatitude { get; set; }
    public double? DropoffLongitude { get; set; }
    public DateOnly PickupDate { get; set; }
    public TimeOnly PickupTime { get; set; }
    [Required] public string VehicleType { get; set; } = string.Empty;
    [Range(1, 12)] public int PassengerCount { get; set; }
    [Range(0, 12)] public int KidsCount { get; set; }
    [Range(0, 30)] public int BaggageCount { get; set; }
    [StringLength(1500)] public string? Preferences { get; set; }
    [Required, StringLength(120)] public string CustomerName { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Phone { get; set; } = string.Empty;
    [EmailAddress, StringLength(160)] public string? Email { get; set; }
}

public record CreateBookingResponse(Guid BookingId, string BookingReference);
