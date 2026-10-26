using RoomTrack.Accommodations.Interfaces.ACL;
using RoomTrack.Bookings.Application.Internal.Configuration;
using RoomTrack.Bookings.Application.OutboundServices;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Domain.Repositories;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Profiles.Interfaces.ACL;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.Bookings.Application.Internal.CommandServices;

/// <summary>
///     Digital check-in and check-out, part of the Bookings context because it is a step of the booking lifecycle: the
///     booking becomes CheckedIn in the same transaction that approves the document, occupies the room (through the
///     Accommodations ACL) and issues the access code. Housekeeping is notified by the <c>GuestCheckedInEvent</c>
///     handler, in the same transaction (e-mail through the outbox).
/// </summary>
public class CheckInService(
    IBookingRepository bookingRepository,
    IDigitalCheckInRepository checkInRepository,
    IDocumentStorage documentStorage,
    IAccommodationsContextFacade accommodationsContextFacade,
    IGuestProfilesContextFacade guestProfilesContextFacade,
    IRoomAvailabilityQueryService roomAvailabilityQueryService,
    ISecretProtector secretProtector,
    HotelCalendar calendar,
    IUnitOfWork unitOfWork) : ICheckInService
{
    private const string AccessCodePurpose = "Bookings.RoomAccessCode";

    public async Task<CheckInResult> Handle(CompleteDigitalCheckInCommand command)
    {
        var requester = await ResolveGuestProfileAsync(command.Requester);
        var booking = await bookingRepository.FindByIdAsync(command.BookingId);
        // The guest of the booking, or the front desk of its hotel for a guest at the desk (assisted check-in).
        if (booking is null || !(booking.IsOwnedBy(requester) || IsFrontDeskOf(booking, requester)))
            throw new BookingNotFoundException(command.BookingId);

        var today = calendar.Today;
        booking.EnsureCheckInAllowed(today);

        // Automatic validation of the document format (type/number/nationality and the file itself).
        var identity = new GuestIdentityDocument(command.DocumentType, command.DocumentNumber, command.Nationality);
        var file = IdentityDocumentFile.Inspect(command.DocumentContent);

        var now = calendar.Now;
        var accessCode = RoomAccessCode.Generate(calendar.CheckOutInstant(booking.CheckOutDate));
        DigitalCheckIn? checkIn = null;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var documentId = await documentStorage.SaveAsync(command.DocumentContent, file.ContentType);
            checkIn = DigitalCheckIn.Approve(booking, identity, file, documentId,
                secretProtector.Protect(AccessCodePurpose, accessCode.Value), accessCode.ValidUntil, now);
            booking.CheckIn(today, now);
            await checkInRepository.AddAsync(checkIn);

            // The room becomes Occupied (it must be Available); the history records the check-in. The room
            // lives in the Accommodations service, which commits it on its own: it is asked last, once every local
            // rule passed, so a refusal there (409) rolls this check-in back and nothing is left half-done.
            await accommodationsContextFacade.OccupyRoomForCheckInAsync(booking.RoomId, requester.UserId, requester.DisplayName);
            await unitOfWork.CompleteAsync();
        });
        return new CheckInResult(booking, checkIn!, accessCode);
    }

    public async Task Handle(RequestCheckInAssistanceCommand command)
    {
        var requester = await ResolveGuestProfileAsync(command.Requester);
        var booking = await bookingRepository.FindByIdAsync(command.BookingId)
                      ?? throw new BookingNotFoundException(command.BookingId);
        booking.RequestCheckInAssistance(requester, command.Message, calendar.Today, calendar.Now);
        await unitOfWork.CompleteAsync();
    }

    public async Task<CheckInResult?> FindAsync(int bookingId, BookingRequester requester)
    {
        requester = await ResolveGuestProfileAsync(requester);
        var booking = await bookingRepository.FindByIdAsync(bookingId);
        if (booking is null || !booking.IsVisibleTo(requester)) return null;

        var checkIn = await checkInRepository.FindByBookingIdAsync(bookingId);
        if (checkIn is null) return null;

        var code = booking.IsOwnedBy(requester)
            ? new RoomAccessCode(secretProtector.Unprotect(AccessCodePurpose, checkIn.AccessCodeProtected), checkIn.AccessCodeValidUntil)
            : null;
        return new CheckInResult(booking, checkIn, code);
    }

    public async Task<Booking> Handle(CheckOutCommand command)
    {
        var requester = await ResolveGuestProfileAsync(command.Requester);
        var booking = await bookingRepository.FindByIdAsync(command.BookingId)
                      ?? throw new BookingNotFoundException(command.BookingId);

        var now = calendar.Now;
        booking.CheckOut(requester, calendar.Today, now);
        (await checkInRepository.FindByBookingIdAsync(booking.Id))?.EndAccess(now);
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // The room goes to Cleaning ("pendiente de limpieza") and gets its cleaning task. Accommodations commits it
            // on its own, so it is asked last, once every local rule passed.
            await accommodationsContextFacade.VacateRoomAfterCheckOutAsync(booking.RoomId, requester.UserId, requester.DisplayName);
            await unitOfWork.CompleteAsync();
        });
        return booking;
    }

    public async Task<IReadOnlyList<AvailableRoom>> FindAlternativeRoomsAsync(int bookingId, BookingRequester requester)
    {
        var booking = await bookingRepository.FindByIdAsync(bookingId);
        if (booking is null || !IsFrontDeskOf(booking, requester)) throw new BookingNotFoundException(bookingId);
        booking.EnsureCheckInAllowed(calendar.Today);

        var current = await accommodationsContextFacade.FetchRoomAsync(booking.RoomId);
        var stay = new DateRange(calendar.Today, booking.CheckOutDate);
        var free = await roomAvailabilityQueryService.Handle(new GetAvailableRoomsQuery(booking.HotelId, stay));
        return free
            .Where(room => room.Room.RoomId != booking.RoomId && room.Room.Status == ReadyRoomStatus)
            .OrderByDescending(room => room.Room.RoomTypeId == current?.RoomTypeId)
            .ThenByDescending(room => room.Room.PricePerNight == booking.PricePerNight)
            .ThenBy(room => Math.Abs(room.Room.PricePerNight - booking.PricePerNight))
            .ThenBy(room => room.Room.Number)
            .ToList();
    }

    /// <summary>The status of a room the guest can move into right now (Accommodations' <c>RoomStatus.Available</c>).</summary>
    private const string ReadyRoomStatus = "Available";

    private static bool IsFrontDeskOf(Booking booking, BookingRequester requester) =>
        !requester.IsGuest && requester.OperatesHotel(booking.HotelId);

    private async Task<BookingRequester> ResolveGuestProfileAsync(BookingRequester requester) =>
        requester.IsGuest
            ? requester.WithGuestProfile(await guestProfilesContextFacade.FetchGuestProfileIdByUserIdAsync(requester.UserId))
            : requester;
}
