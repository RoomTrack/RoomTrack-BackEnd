using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Bookings.Domain.Model.Exceptions;

/// <summary>
///     The booking does not exist, or it is not visible to the requester: a guest never learns about bookings
///     that are not theirs.
/// </summary>
public class BookingNotFoundException(int bookingId) : EntityNotFoundException("Booking", bookingId);

/// <summary>R1 /: an active booking of the same room already covers some of the requested nights (overbooking).</summary>
public class RoomNotAvailableException(int roomId, DateRange dates)
    : BusinessRuleViolationException(BookingErrorCodes.RoomUnavailable, $"Room {roomId} is no longer available for {dates}: another booking holds some of those nights. Search again for available rooms.");

/// <summary>
///     The guest arrived but their room is not ready yet (US-13): it is still being cleaned, the previous guest has not
///     left, or it is under maintenance. <c>params</c>: <c>roomId</c>, <c>roomStatus</c>, <c>estimatedReadyAt</c> and
///     <c>estimatedWaitMinutes</c> (null when it cannot be estimated: then the front desk offers another room, US-27).
/// </summary>
public class RoomNotReadyForCheckInException : BusinessRuleViolationException
{
    public RoomNotReadyForCheckInException(int roomId, string roomStatus, DateTimeOffset? estimatedReadyAt, int? estimatedWaitMinutes)
        : base(BookingErrorCodes.CheckInRoomNotReady, estimatedWaitMinutes is { } minutes
            ? $"Your room is not ready yet: it should be ready in about {minutes} minutes."
            : "Your room is not ready yet. Ask the front desk for help.")
    {
        RoomStatus = roomStatus;
        EstimatedReadyAt = estimatedReadyAt;
        EstimatedWaitMinutes = estimatedWaitMinutes;
        Parameters = new Dictionary<string, object?>
        {
            ["roomId"] = roomId,
            ["roomStatus"] = roomStatus,
            ["estimatedReadyAt"] = estimatedReadyAt,
            ["estimatedWaitMinutes"] = estimatedWaitMinutes
        };
    }

    public string RoomStatus { get; }
    public DateTimeOffset? EstimatedReadyAt { get; }
    public int? EstimatedWaitMinutes { get; }
}

/// <summary>A room under maintenance is never booked.</summary>
public class RoomUnderMaintenanceException(int roomId)
    : BusinessRuleViolationException(BookingErrorCodes.RoomUnderMaintenance, $"Room {roomId} is under maintenance and cannot be booked. Search again for available rooms.");

/// <summary>
///     A hotel accepts bookings only once guests know how to pay them (its administrator set at least one
///     payment method). <c>params.hotelId</c> names the hotel.
/// </summary>
public class HotelNotAcceptingBookingsException : BusinessRuleViolationException
{
    public HotelNotAcceptingBookingsException(int hotelId)
        : base(BookingErrorCodes.HotelPaymentSettingsMissing,
            $"Hotel {hotelId} does not accept bookings yet: its administrator has not set up the payment methods.")
    {
        Parameters = new Dictionary<string, object?> { ["hotelId"] = hotelId };
    }
}

/// <summary>The staff member works for another hotel (R4).</summary>
public class BookingOutsideHotelScopeException()
    : RoomTrack.Shared.Domain.Model.Exceptions.OperationNotAllowedException(BookingErrorCodes.OutsideHotelScope, "You can only manage the bookings of your hotel.");

/// <summary>The booking's current status does not allow the requested transition.</summary>
public class InvalidBookingTransitionException(string code, string message) : BusinessRuleViolationException(code, message);
