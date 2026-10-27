using RoomTrack.Accommodations.Interfaces.ACL;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.IAM.Interfaces.ACL;

namespace RoomTrack.Bookings.Application.OutboundServices;

/// <summary>E-mails to the hotel staff about digital check-ins. Enlisted in the outbox inside the transaction of the check-in.</summary>
public interface ICheckInNotificationService
{
    /// <summary>The guest checked in; housekeeping is informed.</summary>
    Task SendGuestCheckedInAsync(IReadOnlyList<UserContact> recipients, Booking booking, BookingPlace place);

    /// <summary>The guest asked for help with the check-in.</summary>
    Task SendCheckInAssistanceRequestedAsync(IReadOnlyList<UserContact> recipients, Booking booking, BookingPlace place, string? message);
}
