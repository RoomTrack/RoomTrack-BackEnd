using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Reqnroll;
using RoomTrack.Accommodations.Interfaces.ACL;
using RoomTrack.Bookings.API.Infrastructure.Persistence;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Payments.Application.Internal.CommandServices;
using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.Commands;
using RoomTrack.Payments.Domain.Model.Exceptions;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Payments.Infrastructure.Gateways;
using RoomTrack.Payments.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Shared.Domain.Repositories;
using static RoomTrack.Tests.Bookings.BookingTestData;

namespace RoomTrack.Tests.Features.Steps;

/// <summary>
///     Steps of the card payment scenarios (US-15 E01, E02) through the real card payment use case
///     (<see cref="PaymentCommandService"/>) and the sandbox card gateway (<see cref="SandboxCardPaymentGateway"/>), on
///     an in-memory database. Each request runs with its own database context, as in the deployed service.
/// </summary>
[Binding]
public sealed class CardPaymentSteps : IDisposable
{
    private readonly string _database = Guid.NewGuid().ToString();
    private readonly List<BookingsDbContext> _contexts = [];
    private int _bookingId;
    private string _cardToken = SandboxCardPaymentGateway.ApprovedVisa;
    private Payment? _payment;
    private CardPaymentDeclinedException? _declined;

    public void Dispose() => _contexts.ForEach(context => context.Dispose());

    // ── Given ───────────────────────────────────────────────────────

    [Given("que estoy completando mi check-in")]
    public void GivenIAmCheckingIn() => _bookingId = Save(PlaceForGuest());

    [Given("que mi método de pago es rechazado")]
    public void GivenMyCardIsDeclined()
    {
        _bookingId = Save(PlaceForGuest());
        _cardToken = SandboxCardPaymentGateway.DeclinedInsufficientFunds;
    }

    // ── When ────────────────────────────────────────────────────────

    [When("ingreso los datos de mi tarjeta")]
    public async Task WhenIEnterMyCard() => _payment = await Pay(_cardToken);

    [When("esto ocurre")]
    public async Task WhenItHappens()
    {
        try { await Pay(_cardToken); }
        catch (CardPaymentDeclinedException e) { _declined = e; }
    }

    // ── Then ────────────────────────────────────────────────────────

    [Then("el sistema procesa el pago de forma segura mediante la pasarela de pagos integrada")]
    public void ThenThePaymentIsProcessedByTheGateway()
    {
        _payment!.Status.Should().Be(PaymentStatus.Completed);
        _payment.Method.Should().Be(PaymentMethod.OnlineCard);
        _payment.Amount.Should().Be(StoredBooking().TotalPrice, "the gateway charges the booking total");
        _payment.TransactionId.Should().StartWith("chr_sbx_", "the reference is the gateway's charge");
        // Only the gateway's token reached the API: the payment keeps the brand and last digits, never the card number.
        _payment.OperationNumber.Should().Be("VISA •••• 4242");
        _payment.RecordedByUserId.Should().BeNull("nobody of the staff registered it");
        StoredBooking().Status.Should().Be(BookingStatus.Confirmed, "paying confirms the booking and enables the check-in");
    }

    [Then("el sistema me notifica el motivo y me permite intentar otro medio de pago")]
    public async Task ThenIAmToldWhyAndCanTryAnotherWay()
    {
        _declined.Should().NotBeNull("the card was declined");
        _declined!.Code.Should().Be(PaymentErrorCodes.CardDeclined);
        _declined.DeclineCode.Should().Be("insufficient_funds");
        _declined.Reason.Should().Be("La tarjeta no tiene fondos suficientes.");
        _declined.Alternatives.Should().StartWith(nameof(PaymentMethod.OnlineCard)).And
            .Contain([nameof(PaymentMethod.Yape), nameof(PaymentMethod.Plin), nameof(PaymentMethod.Cash)]);

        NewContext().Set<Payment>().Single(p => p.Id == _declined.PaymentId).Status.Should().Be(PaymentStatus.Failed,
            "the failed attempt is kept");
        StoredBooking().Status.Should().Be(BookingStatus.Pending, "the booking can still be paid");

        // Another card, right away.
        var retry = await Pay(SandboxCardPaymentGateway.ApprovedMastercard);
        retry.Status.Should().Be(PaymentStatus.Completed);
        StoredBooking().Status.Should().Be(BookingStatus.Confirmed);
    }

    // ── Support ─────────────────────────────────────────────────────

    private Task<Payment> Pay(string token)
    {
        var context = NewContext();
        var service = new PaymentCommandService(new PaymentRepository(context), new BookingsFacade(context),
            new NotUsedManualGateway(), new SandboxCardPaymentGateway(), new HotelWithWallets(), new ContextUnitOfWork(context),
            TimeProvider.System, NullLogger<PaymentCommandService>.Instance);
        return service.Handle(new PayBookingByCardCommand(_bookingId, GuestUserId, token));
    }

    private int Save(Booking booking)
    {
        var context = NewContext();
        context.Add(booking);
        context.SaveChanges();
        return booking.Id;
    }

    private Booking StoredBooking() => NewContext().Set<Booking>().AsNoTracking().Single(b => b.Id == _bookingId);

    private BookingsDbContext NewContext()
    {
        var context = new BookingsDbContext(new DbContextOptionsBuilder<BookingsDbContext>().UseInMemoryDatabase(_database).Options);
        _contexts.Add(context);
        return context;
    }

    /// <summary>Saves on <c>CompleteAsync</c>; the in-memory provider has no transactions.</summary>
    private sealed class ContextUnitOfWork(BookingsDbContext context) : IUnitOfWork
    {
        public Task CompleteAsync() => context.SaveChangesAsync();
        public Task ExecuteInTransactionAsync(Func<Task> work) => work();
    }

    /// <summary>The Bookings ACL as the Payments context uses it: confirming the booking commits the payment with it.</summary>
    private sealed class BookingsFacade(BookingsDbContext context) : IBookingsContextFacade
    {
        public async Task<BookingSnapshot?> FetchBookingAsync(int bookingId, int? guestUserId = null)
        {
            var booking = await context.Set<Booking>().FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking is null || (guestUserId is { } guest && booking.GuestId?.Value != guest)) return null;
            return new BookingSnapshot(booking.Id, booking.RoomId, booking.CheckInDate, booking.CheckOutDate, booking.Nights,
                booking.Status.ToString(), booking.CanBePaid, booking.HotelId, booking.Code.Value, booking.TotalPrice,
                booking.GuestEmail, booking.GuestName);
        }

        public async Task<bool> ConfirmBookingAsync(int bookingId)
        {
            var booking = await context.Set<Booking>().SingleAsync(b => b.Id == bookingId);
            booking.Confirm(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
            return true;
        }

        public Task<IReadOnlyList<int>> FetchBookingIdsOfHotelAsync(int hotelId) => throw new NotSupportedException();
    }

    private sealed class NotUsedManualGateway : RoomTrack.Payments.Application.OutboundServices.IPaymentGateway
    {
        public Task<RoomTrack.Payments.Application.OutboundServices.PaymentGatewayResult> ChargeAsync(
            RoomTrack.Payments.Application.OutboundServices.PaymentCharge charge, System.Threading.CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("A card payment never goes through the manual gateway.");
    }

    /// <summary>A hotel that takes Yape and Plin (no bank account).</summary>
    private sealed class HotelWithWallets : IAccommodationsContextFacade
    {
        public Task<HotelPaymentInstructions?> FetchPaymentInstructionsAsync(int hotelId) =>
            Task.FromResult<HotelPaymentInstructions?>(new HotelPaymentInstructions("Hotel Demo SAC", "987654321", "987654321",
                null, null, null));

        public Task<RoomOffer?> FetchRoomAsync(int roomId) => throw new NotSupportedException();
        public Task OccupyRoomForCheckInAsync(int roomId, int? guestUserId, string? guestEmail) => throw new NotSupportedException();
        public Task VacateRoomAfterCheckOutAsync(int roomId, int? checkedOutByUserId, string? checkedOutByEmail) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, string>> FetchRoomNumbersAsync(IReadOnlyCollection<int> roomIds) => throw new NotSupportedException();
        public Task<HotelSummary?> FetchHotelAsync(int hotelId) => throw new NotSupportedException();
        public Task<bool> HotelExistsAsync(int hotelId) => throw new NotSupportedException();
        public Task<bool> RoomExistsAsync(int roomId) => throw new NotSupportedException();
        public Task<decimal?> FetchRoomPricePerNightAsync(int roomId) => throw new NotSupportedException();
        public Task<int?> FetchHotelIdOfRoomAsync(int roomId) => throw new NotSupportedException();
        public Task<RoomOffer?> LockRoomForBookingAsync(int roomId) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoomOffer>> FetchRoomsOfferedForBookingAsync(int? hotelId) => throw new NotSupportedException();
    }
}
