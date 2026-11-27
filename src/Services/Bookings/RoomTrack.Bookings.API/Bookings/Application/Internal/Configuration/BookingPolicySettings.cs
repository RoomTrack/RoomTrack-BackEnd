using System.ComponentModel.DataAnnotations;

namespace RoomTrack.Bookings.Application.Internal.Configuration;

/// <summary>Booking rules that depend on the operation (section <c>Bookings</c>, env vars <c>Bookings__*</c>).</summary>
public class BookingPolicySettings : IValidatableObject
{
    public const string SectionName = "Bookings";

    /// <summary>
    ///     IANA time zone of the hotels: decides "today" for the date rules (no check-in in the past, no cancellation
    ///     on the check-in day, check-in window).
    /// </summary>
    [Required]
    public string TimeZone { get; set; } = "America/Lima";

    /// <summary>Hours a Pending booking waits for its payment before it expires (payment hold, D1).</summary>
    [Range(1, 168)]
    public int PaymentHoldHours { get; set; } = 24;

    /// <summary>Check-out time (hotel time, HH:mm): the room access code stops working then.</summary>
    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Bookings:CheckOutTime must be HH:mm.")]
    public string CheckOutTime { get; set; } = "12:00";

    /// <summary>Check-in time (hotel time, HH:mm): the arriving guests expect their room ready from then on (US-23).</summary>
    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Bookings:CheckInTime must be HH:mm.")]
    public string CheckInTime { get; set; } = "15:00";

    /// <summary>
    ///     Days before the check-in day a guest can already do the digital check-in (US-13, "check-in en proceso"): at
    ///     the hotel they only confirm their identity. 0 turns it off.
    /// </summary>
    [Range(0, 30)]
    public int PreArrivalCheckInDays { get; set; } = 3;

    public TimeSpan PaymentHold => TimeSpan.FromHours(PaymentHoldHours);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out _))
            yield return new ValidationResult($"Bookings:TimeZone '{TimeZone}' is not a known time zone.", [nameof(TimeZone)]);
    }
}
