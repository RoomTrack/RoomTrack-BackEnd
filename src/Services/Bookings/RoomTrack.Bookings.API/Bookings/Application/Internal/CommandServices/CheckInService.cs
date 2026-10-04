using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Bookings.Application.Internal.Configuration;
using BackendAwRoomTrack.API.Bookings.Application.OutboundServices;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Commands;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Bookings.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Bookings.Domain.Repositories;
using BackendAwRoomTrack.API.Bookings.Domain.Services;
using BackendAwRoomTrack.API.Profiles.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Application.OutboundServices;
using BackendAwRoomTrack.API.Shared.Domain.Repositories;

namespace BackendAwRoomTrack.API.Bookings.Application.Internal.CommandServices;

/// <summary>
///     Digital check-in, part of the Bookings context because it is a step of the booking lifecycle: the
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
    ISecretProtector secretProtector,
    HotelCalendar calendar,
    IUnitOfWork unitOfWork) : ICheckInService
{
    private const string AccessCodePurpose = "Bookings.RoomAccessCode";

    public async Task<CheckInResult> Handle(CompleteDigitalCheckInCommand command)
    {
        var requester = await ResolveGuestProfileAsync(command.Requester);
        var booking = await bookingRepository.FindByIdAsync(command.BookingId);
        if (booking is null || !booking.IsOwnedBy(requester))
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

    private async Task<BookingRequester> ResolveGuestProfileAsync(BookingRequester requester) =>
        requester.IsGuest
            ? requester.WithGuestProfile(await guestProfilesContextFacade.FetchGuestProfileIdByUserIdAsync(requester.UserId))
            : requester;
}
