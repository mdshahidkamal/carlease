namespace CarLease.Api.Models;

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Pickup { get; set; } = string.Empty;
    public string? PickupPlaceId { get; set; }
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    public string? Via { get; set; }
    public string Dropoff { get; set; } = string.Empty;
    public string? DropoffPlaceId { get; set; }
    public double? DropoffLatitude { get; set; }
    public double? DropoffLongitude { get; set; }
    public decimal? EstimatedDistanceMiles { get; set; }
    public decimal EstimatedAmountDollars { get; set; }
    public DateTime PickupDateTime { get; set; }
    public string VehicleType { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public int PassengerCount { get; set; }
    public int KidsCount { get; set; }
    public int BaggageCount { get; set; }
    public string? Preferences { get; set; }
    public string Status { get; set; } = BookingStatuses.PendingReview;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
