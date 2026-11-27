using RoomTrack.Accommodations.Interfaces.ACL;
using RoomTrack.Payments.Domain.Model.Exceptions;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Payments.Application.OutboundServices;
using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.Commands;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Payments.Domain.Repositories;
using RoomTrack.Payments.Domain.Services;
using RoomTrack.Shared.Domain.Repositories;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Payments.Application.Internal.CommandServices;

/// <summary>
///     Registers booking payments through the <see cref="IPaymentGateway"/> port and confirms the booking (D1).
/// </summary>
/// <remarks>
///     Payments never touches the Bookings repositories: it reads the booking through its ACL facade and confirms it
///     through the Bookings application layer <b>in the same transaction</b>, so a payment is never saved without its
///     booking being confirmed (nor the other way round). The <c>PaymentCompletedEvent</c> is still published after the
///     commit for any other subscriber.
/// </remarks>
public class PaymentCommandService(
    IPaymentRepository paymentRepository,
    IBookingsContextFacade bookingsContextFacade,
    IPaymentGateway paymentGateway,
    ICardPaymentGateway cardPaymentGateway,
    IAccommodationsContextFacade accommodationsContextFacade,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<PaymentCommandService> logger)
    : IPaymentCommandService
{
    public async Task<Payment> Handle(RegisterPaymentCommand command)
    {
        Payment? payment = null;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var booking = await bookingsContextFacade.FetchBookingAsync(command.BookingId)
                          ?? throw new EntityNotFoundException("Booking", command.BookingId);
            if (!command.AllHotels && command.StaffHotelId != booking.HotelId)
                throw new OperationNotAllowedException(PaymentErrorCodes.OutsideHotelScope, "You can only register payments of the bookings of your hotel.");
            if (await paymentRepository.ExistsCompletedForBookingAsync(booking.BookingId))
                throw new BusinessRuleViolationException(PaymentErrorCodes.BookingAlreadyPaid, $"Booking {booking.Code} is already paid.");
            if (!booking.CanBePaid)
                throw new BusinessRuleViolationException(PaymentErrorCodes.BookingNotPending,
                    $"Booking {booking.Code} is {booking.Status.ToLowerInvariant()}: only a pending booking can be paid.");

            var now = timeProvider.GetUtcNow();
            payment = Payment.Register(booking.BookingId, booking.TotalPrice, command.Method, command.OperationNumber,
                command.Note, command.StaffUserId, now);

            var result = await paymentGateway.ChargeAsync(new PaymentCharge(booking.BookingId, booking.Code,
                payment.Amount, payment.Method, payment.OperationNumber));
            await paymentRepository.AddAsync(payment);

            if (!result.Approved)
            {
                payment.Fail(result.FailureReason ?? "Rejected by the payment gateway.");
                await unitOfWork.CompleteAsync();
                return;
            }

            payment.Complete(result.TransactionReference, now);
            // Commits the payment and the booking confirmation together (same unit of work and transaction).
            await bookingsContextFacade.ConfirmBookingAsync(booking.BookingId);
            logger.LogInformation("Booking {BookingCode} confirmed by payment {Reference} ({Amount}, {Method}).",
                booking.Code, result.TransactionReference, payment.Amount, payment.Method);
        });
        return payment!;
    }

    /// <summary>Longest card token accepted (the gateways issue tokens of a few dozen characters).</summary>
    public const int MaxCardTokenLength = 100;

    public async Task<Payment> Handle(PayBookingByCardCommand command)
    {
        var token = command.CardToken?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length > MaxCardTokenLength)
            throw new InvalidFieldException("cardToken", PaymentErrorCodes.CardTokenInvalid,
                $"Send the token of the card issued by the card gateway (at most {MaxCardTokenLength} characters).");

        Payment? payment = null;
        CardChargeResult? result = null;
        BookingSnapshot? booking = null;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Only the guest who owns the booking (anyone else gets 404, as if it did not exist).
            booking = await bookingsContextFacade.FetchBookingAsync(command.BookingId, command.GuestUserId)
                      ?? throw new EntityNotFoundException("Booking", command.BookingId);
            if (await paymentRepository.ExistsCompletedForBookingAsync(booking.BookingId))
                throw new BusinessRuleViolationException(PaymentErrorCodes.BookingAlreadyPaid, $"Booking {booking.Code} is already paid.");
            if (!booking.CanBePaid)
                throw new BusinessRuleViolationException(PaymentErrorCodes.BookingNotPending,
                    $"Booking {booking.Code} is {booking.Status.ToLowerInvariant()}: only a pending booking can be paid.");

            var now = timeProvider.GetUtcNow();
            payment = Payment.RegisterOnlineCard(booking.BookingId, booking.TotalPrice, now);
            // Same booking and token, same charge: a retried request is never charged twice by the gateway.
            result = await cardPaymentGateway.ChargeAsync(new CardCharge(booking.BookingId, booking.Code, payment.Amount, token,
                $"booking:{booking.BookingId}:{token}"));
            await paymentRepository.AddAsync(payment);

            if (!result.Approved)
            {
                // The failed attempt is kept (the payment history shows it); the booking stays Pending.
                payment.Fail($"{result.DeclineCode}: {result.DeclineReason}");
                await unitOfWork.CompleteAsync();
                return;
            }

            payment.Complete(result.Reference!, now, result.CardLabel);
            // Commits the payment and the booking confirmation together (same unit of work and transaction).
            await bookingsContextFacade.ConfirmBookingAsync(booking.BookingId);
            logger.LogInformation("Booking {BookingCode} confirmed by the card payment {Reference} ({Amount}).",
                booking.Code, result.Reference, payment.Amount);
        });

        // Thrown after the commit, so the failed attempt stays recorded.
        if (result is { Approved: false })
            throw new CardPaymentDeclinedException(payment!.Id, result.DeclineCode ?? "card_declined",
                result.DeclineReason ?? "The card was declined.", await AlternativesAsync(booking!.HotelId));
        return payment!;
    }

    /// <summary>
    ///     How a guest whose card was declined can still pay: another card, the hotel's Yape, Plin or transfer (when it
    ///     offers them) and, at the front desk, cash or the POS.
    /// </summary>
    private async Task<IReadOnlyList<string>> AlternativesAsync(int hotelId)
    {
        var methods = new List<string> { nameof(PaymentMethod.OnlineCard) };
        HotelPaymentInstructions? instructions = null;
        try
        {
            instructions = await accommodationsContextFacade.FetchPaymentInstructionsAsync(hotelId);
        }
        catch (HttpRequestException e)
        {
            logger.LogWarning(e, "The payment methods of hotel {HotelId} could not be read.", hotelId);
        }
        if (!string.IsNullOrWhiteSpace(instructions?.YapeNumber)) methods.Add(nameof(PaymentMethod.Yape));
        if (!string.IsNullOrWhiteSpace(instructions?.PlinNumber)) methods.Add(nameof(PaymentMethod.Plin));
        if (!string.IsNullOrWhiteSpace(instructions?.BankAccountNumber) || !string.IsNullOrWhiteSpace(instructions?.BankAccountCci))
            methods.Add(nameof(PaymentMethod.BankTransfer));
        methods.Add(nameof(PaymentMethod.Cash));
        methods.Add(nameof(PaymentMethod.CardAtFrontDesk));
        return methods;
    }
}
