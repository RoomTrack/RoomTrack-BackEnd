using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Bookings.Application.OutboundServices;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Events;
using BackendAwRoomTrack.API.Bookings.Domain.Repositories;
using BackendAwRoomTrack.API.IAM.Domain.Model.Constants;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Application.Internal.EventHandlers;

namespace BackendAwRoomTrack.API.Bookings.Application.Internal.EventHandlers;

/// <summary>
///     A completed check-in notifies the housekeeping staff of the hotel and a request for help
///     notifies the front desk (the hotel admins when the hotel has no reception user).
/// </summary>
public class CheckInStaffNotificationHandler(
    IBookingRepository bookingRepository,
    IAccommodationsContextFacade accommodationsContextFacade,
    IIamContextFacade iamContextFacade,
    ICheckInNotificationService notifications) :
    IDomainEventHandler<GuestCheckedInEvent>,
    IDomainEventHandler<CheckInAssistanceRequestedEvent>
{
    public async Task HandleAsync(GuestCheckedInEvent e, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.FindByIdAsync(e.BookingId);
        if (booking is null) return;
        var housekeeping = await iamContextFacade.ListHotelStaffAsync(e.HotelId, [UserRoles.Housekeeping]);
        await notifications.SendGuestCheckedInAsync(housekeeping, booking, new BookingPlace(await accommodationsContextFacade.FetchHotelAsync(e.HotelId), (await accommodationsContextFacade.FetchRoomAsync(booking.RoomId))?.Number ?? booking.RoomId.ToString()));
    }

    public async Task HandleAsync(CheckInAssistanceRequestedEvent e, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.FindByIdAsync(e.BookingId);
        if (booking is null) return;
        var frontDesk = await iamContextFacade.ListHotelStaffAsync(e.HotelId, [UserRoles.Reception]);
        if (frontDesk.Count == 0)
            frontDesk = await iamContextFacade.ListHotelStaffAsync(e.HotelId, [UserRoles.Admin]);
        await notifications.SendCheckInAssistanceRequestedAsync(frontDesk, booking,
            new BookingPlace(await accommodationsContextFacade.FetchHotelAsync(e.HotelId), (await accommodationsContextFacade.FetchRoomAsync(booking.RoomId))?.Number ?? booking.RoomId.ToString()), e.Message);
    }
}
