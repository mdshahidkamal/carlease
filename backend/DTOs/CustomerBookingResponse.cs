namespace CarLease.Api.DTOs;

public record CustomerBookingSummary(string BookingReference, DateTime PickupDateTime, string Status);

public record CustomerBookingDetails(
    string BookingReference, string Pickup, string? Via, string Dropoff, DateTime PickupDateTime, string Status,
    string VehicleType, int PassengerCount, int KidsCount, int BaggageCount,
    decimal? EstimatedDistanceMiles, decimal EstimatedAmountDollars, string? Preferences);
