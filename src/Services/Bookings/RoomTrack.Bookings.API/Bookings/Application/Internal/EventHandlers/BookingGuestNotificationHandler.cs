using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Bookings.Application.OutboundServices;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Events;
using BackendAwRoomTrack.API.Bookings.Domain.Repositories;
using BackendAwRoomTrack.API.Shared.Application.Internal.EventHandlers;

namespace BackendAwRoomTrack.API.Bookings.Application.Internal.EventHandlers;

/// <summary>
///     Keeps the guest informed of every change of their booking (D1).
///     Runs inside the transaction of the change and the e-mails go through the outbox, so an e-mail never
///     announces a change that was rolled back.
/// </summary>
public class BookingGuestNotificationHandler(
    IBookingRepository bookingRepository,
    IAccommodationsContextFacade accommodationsContextFacade,
    IBookingNotificationService notifications) :
    IDomainEventHandler<BookingCreatedEvent>,
    IDomainEventHandler<BookingConfirmedEvent>,
    IDomainEventHandler<BookingCancelledEvent>,
    IDomainEventHandler<BookingRescheduledEvent>
{
    public async Task HandleAsync(BookingCreatedEvent e, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.FindByIdAsync(e.BookingId);
        if (booking is null) return;
        // How to pay: the payment methods of the booking's hotel, from the Accommodations context.
        await notifications.SendBookingPlacedAsync(booking, await PlaceAsync(booking),
            await accommodationsContextFacade.FetchPaymentInstructionsAsync(booking.HotelId));
    }

    public async Task HandleAsync(BookingConfirmedEvent e, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.FindByIdAsync(e.BookingId);
        if (booking is null) return;
        await notifications.SendBookingConfirmedAsync(booking, await PlaceAsync(booking));
    }

    public async Task HandleAsync(BookingCancelledEvent e, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.FindByIdAsync(e.BookingId);
        if (booking is null) return;
        await notifications.SendBookingCancelledAsync(booking, await PlaceAsync(booking));
    }

    public async Task HandleAsync(BookingRescheduledEvent e, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.FindByIdAsync(e.BookingId);
        if (booking is null) return;
        await notifications.SendBookingRescheduledAsync(booking, await PlaceAsync(booking),
            e.PreviousCheckIn, e.PreviousCheckOut, await RoomNumberAsync(e.PreviousRoomId));
    }

    private async Task<BookingPlace> PlaceAsync(Domain.Model.Aggregates.Booking booking) =>
        new(await accommodationsContextFacade.FetchHotelAsync(booking.HotelId), await RoomNumberAsync(booking.RoomId));

    private async Task<string> RoomNumberAsync(int roomId) =>
        (await accommodationsContextFacade.FetchRoomAsync(roomId))?.Number ?? roomId.ToString();
}
