using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;

namespace BackendAwRoomTrack.API.Bookings.Application.OutboundServices;

/// <summary>E-mails to the hotel staff about digital check-ins. Enlisted in the outbox inside the transaction of the check-in.</summary>
public interface ICheckInNotificationService
{
    /// <summary>The guest checked in; housekeeping is informed.</summary>
    Task SendGuestCheckedInAsync(IReadOnlyList<UserContact> recipients, Booking booking, BookingPlace place);

    /// <summary>The guest asked for help with the check-in.</summary>
    Task SendCheckInAssistanceRequestedAsync(IReadOnlyList<UserContact> recipients, Booking booking, BookingPlace place, string? message);
}
