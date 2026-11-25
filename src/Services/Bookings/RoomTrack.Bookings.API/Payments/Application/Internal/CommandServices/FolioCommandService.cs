using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Payments.Application.OutboundServices;
using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.Commands;
using RoomTrack.Payments.Domain.Model.Exceptions;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Payments.Domain.Repositories;
using RoomTrack.Payments.Domain.Services;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.Payments.Application.Internal.CommandServices;

/// <summary>
///     Use cases of the folio of a stay (US-15/16/17). The booking is read through the Bookings ACL; the folio is
///     opened on first use. Each payment's receipt is issued by the <c>FolioPaymentRecordedEvent</c> handler in the same
///     transaction.
/// </summary>
public class FolioCommandService(
    IGuestFolioRepository folioRepository,
    IReceiptRepository receiptRepository,
    IBookingsContextFacade bookingsContextFacade,
    IReceiptNotificationService notificationService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IFolioCommandService
{
    public async Task<GuestFolio> Handle(PostFolioChargeCommand command)
    {
        var booking = await BookingOfAsync(command.BookingId, command.Actor, staffOnly: true);
        if (!booking.IsStaying)
            throw new BusinessRuleViolationException(FolioErrorCodes.BookingNotStaying,
                $"Booking {booking.Code} is {booking.Status.ToLowerInvariant()}: charges are posted during the stay.");
        var folio = await FolioOfAsync(booking);
        folio.PostCharge(command.Description, command.Category, command.Amount, command.Actor.UserId, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return folio;
    }

    public async Task<GuestFolio> Handle(HoldDepositCommand command)
    {
        var booking = await BookingOfAsync(command.BookingId, command.Actor, staffOnly: true);
        if (!booking.IsUpcomingOrStaying)
            throw new BusinessRuleViolationException(FolioErrorCodes.BookingNotStaying,
                $"Booking {booking.Code} is {booking.Status.ToLowerInvariant()}: the deposit is held at check-in.");
        var folio = await FolioOfAsync(booking);
        folio.HoldDeposit(command.Amount, command.Method, command.OperationNumber, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return folio;
    }

    public async Task<(GuestFolio Folio, IReadOnlyList<Receipt> Receipts)> Handle(PayFolioCommand command)
    {
        var booking = await BookingOfAsync(command.BookingId, command.Actor, staffOnly: false);
        var folio = await FolioOfAsync(booking);
        var payments = folio.Pay(command.Parts, command.Actor.IsGuest ? null : command.Actor.UserId, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();

        var paymentIds = payments.Select(payment => payment.Id).ToHashSet();
        var receipts = (await receiptRepository.ListByBookingIdAsync(booking.BookingId))
            .Where(receipt => receipt.Source == ReceiptSource.FolioPayment && paymentIds.Contains(receipt.SourceId))
            .ToList();
        return (folio, receipts);
    }

    public async Task<GuestFolio> Handle(RequestInvoiceCommand command)
    {
        var booking = await BookingOfAsync(command.BookingId, command.Actor, staffOnly: false);
        var folio = await FolioOfAsync(booking);
        folio.RequestInvoice(InvoiceData.Create(command.Ruc, command.BusinessName, command.Address));
        await unitOfWork.CompleteAsync();
        return folio;
    }

    public async Task<Receipt?> Handle(ResendReceiptCommand command)
    {
        var receipt = await receiptRepository.FindByIdAsync(command.ReceiptId);
        if (receipt is null || !command.Actor.OperatesHotel(receipt.HotelId)) return null;
        await notificationService.SendReceiptAsync(receipt);
        receipt.RecordSent(timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return receipt;
    }

    /// <summary>The booking if the actor may act on it (guest: their own; staff: their hotel), otherwise a 404.</summary>
    private async Task<BookingSnapshot> BookingOfAsync(int bookingId, FolioActor actor, bool staffOnly)
    {
        if (staffOnly && actor.IsGuest) throw new EntityNotFoundException("Booking", bookingId);
        var booking = await bookingsContextFacade.FetchBookingAsync(bookingId, actor.IsGuest ? actor.UserId : null);
        if (booking is null || (!actor.IsGuest && !actor.OperatesHotel(booking.HotelId)))
            throw new EntityNotFoundException("Booking", bookingId);
        return booking;
    }

    /// <summary>The folio of the booking, opened (and saved, so its payments can reference it) on first use.</summary>
    private async Task<GuestFolio> FolioOfAsync(BookingSnapshot booking)
    {
        var folio = await folioRepository.FindByBookingIdAsync(booking.BookingId);
        if (folio is not null) return folio;
        folio = GuestFolio.Open(booking.BookingId, booking.HotelId, booking.Code, booking.GuestName, booking.GuestEmail,
            timeProvider.GetUtcNow());
        await folioRepository.AddAsync(folio);
        await unitOfWork.CompleteAsync();
        return folio;
    }
}
