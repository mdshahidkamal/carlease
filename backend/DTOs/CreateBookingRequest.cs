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
    [Required, Range(typeof(decimal), "0", "9999999999.99")] public decimal? EstimatedDistanceMiles { get; set; }
    [Required] public DateTimeOffset? PickupAtUtc { get; set; }
    [Required] public Guid? VehicleId { get; set; }
    [Range(1, 12)] public int PassengerCount { get; set; }
    [Range(0, 12)] public int KidsCount { get; set; }
    [Required, Range(0, 30)] public int? BaggageCount { get; set; }
    [StringLength(1500)] public string? Preferences { get; set; }
}

public record CreateBookingResponse(Guid BookingId, string BookingReference, string Status);
