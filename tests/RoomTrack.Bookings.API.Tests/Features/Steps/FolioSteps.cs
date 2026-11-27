using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reqnroll;
using RoomTrack.Accommodations.Interfaces.ACL;
using RoomTrack.Bookings.API.Infrastructure.Persistence;
using RoomTrack.Bookings.Application.Internal.CommandServices;
using RoomTrack.Bookings.Application.Internal.Configuration;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.Events;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Payments.Application.ACL;
using RoomTrack.Payments.Application.Internal.CommandServices;
using RoomTrack.Payments.Application.Internal.EventHandlers;
using RoomTrack.Payments.Application.Internal.QueryServices;
using RoomTrack.Payments.Application.OutboundServices;
using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.Commands;
using RoomTrack.Payments.Domain.Model.Events;
using RoomTrack.Payments.Domain.Model.Exceptions;
using RoomTrack.Payments.Domain.Model.Queries;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Payments.Domain.Repositories;
using RoomTrack.Payments.Domain.Services;
using RoomTrack.Payments.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Profiles.Interfaces.ACL;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Events;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Repositories;
using static RoomTrack.Tests.Bookings.BookingTestData;

namespace RoomTrack.Tests.Features.Steps;

/// <summary>
///     Steps of the folio scenarios (US-14 E02/E03, US-15 E03, US-16, US-17, US-27 E02) through the real Payments use
///     cases, the check-out of <see cref="CheckInService"/> and the production event handlers (receipt on every payment,
///     folio closed at check-out), wired with the same dependency injection as the service on an in-memory database.
/// </summary>
[Binding]
public sealed class FolioSteps : IDisposable
{
    private const int RoomId = 101;
    private const int FrontDeskUserId = 7;

    private static readonly BookingRequester GuestRequester = BookingRequester.Guest(GuestUserId, "Alice");
    private static readonly BookingRequester FrontDeskRequester = BookingRequester.HotelStaff(FrontDeskUserId, HotelId);
    private static readonly FolioActor Guest = new(GuestUserId, IsGuest: true, StaffHotelId: null, AllHotels: false);
    private static readonly FolioActor FrontDesk = new(FrontDeskUserId, IsGuest: false, HotelId, AllHotels: false);

    private static readonly DateTime ArrivalDay = Stay(Today).CheckIn;
    private static readonly DateTimeOffset ArrivalAt = new(ArrivalDay.Year, ArrivalDay.Month, ArrivalDay.Day, 20, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, FolioStatus> FolioStatuses = new()
    {
        ["saldado"] = FolioStatus.Settled,
        ["abierto"] = FolioStatus.Open,
        ["cerrado"] = FolioStatus.Closed
    };

    private readonly BookingsDbContext _context;
    private readonly ServiceProvider _services;
    private readonly FixedClock _clock = new(ArrivalAt);
    private readonly RecordingReceiptNotifications _notifications = new();
    private readonly FakeAccommodations _accommodations = new();
    private int _bookingId;
    private Func<Task>? _pending;
    private GuestFolio? _folio;
    private IReadOnlyList<Receipt> _receipts = [];
    private IReadOnlyList<PaymentHistoryEntry> _history = [];
    private DomainException? _error;

    public FolioSteps()
    {
        _context = new BookingsDbContext(new DbContextOptionsBuilder<BookingsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var services = new ServiceCollection();
        services.AddSingleton(_context);
        services.AddSingleton<AppDbContext>(_context);
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton<IDomainEventDispatcher>(sp => new DomainEventDispatcher(sp));
        services.AddSingleton<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IGuestFolioRepository, GuestFolioRepository>();
        services.AddSingleton<IReceiptRepository, ReceiptRepository>();
        services.AddSingleton<IPaymentRepository, PaymentRepository>();
        services.AddSingleton<IBookingsContextFacade>(new BookingsFacade(_context));
        services.AddSingleton<IReceiptNotificationService>(_notifications);
        services.AddSingleton<ReceiptIssuer>();
        services.AddSingleton<IssueReceiptOnPaymentHandler>();
        services.AddSingleton<IDomainEventHandler<PaymentCompletedEvent>>(sp => sp.GetRequiredService<IssueReceiptOnPaymentHandler>());
        services.AddSingleton<IDomainEventHandler<FolioPaymentRecordedEvent>>(sp => sp.GetRequiredService<IssueReceiptOnPaymentHandler>());
        services.AddSingleton<IDomainEventHandler<GuestCheckedOutEvent>, CloseFolioOnCheckOutHandler>();
        services.AddSingleton<IFolioCommandService, FolioCommandService>();
        services.AddSingleton<IFolioQueryService, FolioQueryService>();
        _services = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        _context.Dispose();
    }

    private IFolioCommandService Folios => _services.GetRequiredService<IFolioCommandService>();
    private IFolioQueryService FolioQueries => _services.GetRequiredService<IFolioQueryService>();

    // ── Given ───────────────────────────────────────────────────────

    [Given("que tengo consumos adicionales sin pagar")]
    [Given("que voy a hacer check-out")]
    [Given("que tengo un saldo pendiente")]
    [Given("que un huésped hace check-out en recepción con cargos pendientes")]
    public async Task GivenAStayWithUnpaidCharges()
    {
        AGuestStaying();
        await Charge("Minibar: 2 aguas y 1 snack", FolioChargeCategory.Minibar, 18.50m);
        await Charge("Lavandería (3 prendas)", FolioChargeCategory.Laundry, 30m);
    }

    [Given("que quiero pagar con dos métodos distintos")]
    public async Task GivenABalanceToSplit()
    {
        AGuestStaying();
        await Charge("Cena en el restaurante", FolioChargeCategory.Restaurant, 120m);
    }

    [Given("que completo el check-out")]
    public async Task GivenACompletedCheckOut()
    {
        AGuestStaying(paid: true);
        await Charge("Minibar", FolioChargeCategory.Minibar, 20m);
        await Folios.Handle(new PayFolioCommand(_bookingId, [new FolioPaymentPart(20m, PaymentMethod.Cash, null)], Guest));
        _pending = CheckOutAsGuest;
    }

    [Given("que el hotel requiere un depósito reembolsable")]
    public void GivenTheHotelRequiresADeposit() => AGuestStaying();

    [Given("que no tengo cargos adicionales al depósito de garantía")]
    public async Task GivenOnlyTheDeposit()
    {
        AGuestStaying();
        await Folios.Handle(new HoldDepositCommand(_bookingId, 200m, PaymentMethod.CardAtFrontDesk, "POS-778812", FrontDesk));
    }

    [Given("que un huésped completa un pago")]
    public void GivenAGuestPays()
    {
        _bookingId = Save(PlaceForGuest(roomId: RoomId));
        _pending = PayTheBookingAsync;
    }

    [Given("que un huésped requiere factura")]
    public async Task GivenAGuestNeedsAnInvoice()
    {
        AGuestStaying();
        await Charge("Room service", FolioChargeCategory.RoomService, 59m);
    }

    [Given("que un huésped no recibió su comprobante")]
    public async Task GivenAGuestDidNotGetTheReceipt()
    {
        _bookingId = Save(PlaceForGuest(roomId: RoomId));
        await PayTheBookingAsync();
    }

    [Given("que necesito revisar transacciones pasadas")]
    public async Task GivenPastTransactions()
    {
        // A paid stay with a folio payment, a deposit released at check-out, and a cancelled paid booking (refund).
        GivenAGuestPays();
        await PayTheBookingAsync();
        var booking = _context.Set<Booking>().Single(b => b.Id == _bookingId);
        _clock.Now = ArrivalAt;
        booking.CheckIn(ArrivalDay, ArrivalAt);
        booking.ClearDomainEvents();
        await _context.SaveChangesAsync();
        _accommodations.Rooms[RoomId] = "Occupied";
        await Folios.Handle(new HoldDepositCommand(_bookingId, 150m, PaymentMethod.Cash, null, FrontDesk));
        await Charge("Spa", FolioChargeCategory.Spa, 80m);
        await Folios.Handle(new PayFolioCommand(_bookingId, [new FolioPaymentPart(80m, PaymentMethod.Plin, "PL-1")], Guest));
        _clock.Now = ArrivalAt.AddDays(2);
        await CheckOutAsGuest();

        var cancelled = PlaceForGuest(roomId: 102);
        var cancelledId = Save(cancelled);
        cancelled.Confirm(Now);
        cancelled.ClearDomainEvents();
        var refund = Payment.Register(cancelledId, cancelled.TotalPrice, PaymentMethod.Yape, "YP-9", null, FrontDeskUserId, Now);
        refund.Complete("TX-9", Now);
        _context.Add(refund);
        await _context.SaveChangesAsync();
        refund.Refund(ArrivalAt.AddDays(1));
        await _context.SaveChangesAsync();
    }

    // ── When ────────────────────────────────────────────────────────

    [When("intento hacer check-out")]
    public async Task WhenITryToCheckOut()
    {
        try { await CheckOutAsGuest(); }
        catch (DomainException e) { _error = e; }
    }

    [When("se procesa")]
    public Task WhenItIsProcessed() => _pending!();

    [When("completo el pago")]
    public async Task WhenIPayTheDeposit() =>
        await Folios.Handle(new HoldDepositCommand(_bookingId, 200m, PaymentMethod.CardAtFrontDesk, "POS-778812", FrontDesk));

    [When("reviso mi folio")]
    public async Task WhenIReviewMyFolio() => _folio = await FolioQueries.Handle(new GetFolioByBookingIdQuery(_bookingId, Guest));

    [When("confirmo el pago")]
    public async Task WhenIPayTheBalance() =>
        (_folio, _receipts) = await Folios.Handle(new PayFolioCommand(_bookingId,
            [new FolioPaymentPart(48.50m, PaymentMethod.Yape, "00987654")], Guest));

    [When("distribuyo el monto entre ambos")]
    public async Task WhenISplitThePayment() =>
        (_folio, _receipts) = await Folios.Handle(new PayFolioCommand(_bookingId,
        [
            new FolioPaymentPart(70m, PaymentMethod.Yape, "00987654"),
            new FolioPaymentPart(50m, PaymentMethod.Cash, null)
        ], Guest));

    [When("hago check-out")]
    public Task WhenICheckOut() => CheckOutAsGuest();

    [When("ingresa sus datos fiscales antes del check-out")]
    public async Task WhenTheGuestGivesTheInvoiceData()
    {
        await Folios.Handle(new RequestInvoiceCommand(_bookingId, "20123456789", "Inversiones Andinas S.A.C.",
            "Av. Larco 123, Miraflores", Guest));
        (_folio, _receipts) = await Folios.Handle(new PayFolioCommand(_bookingId,
            [new FolioPaymentPart(59m, PaymentMethod.BankTransfer, "TRF-1")], Guest));
    }

    [When("lo reenvío desde el panel")]
    public async Task WhenTheFrontDeskResendsIt()
    {
        _notifications.Sent.Clear();
        var receipt = (await FolioQueries.Handle(new GetReceiptsByBookingIdQuery(_bookingId, FrontDesk)))!.Single();
        _receipts = [(await Folios.Handle(new ResendReceiptCommand(receipt.Id, FrontDesk)))!];
    }

    [When("accedo al historial de pagos")]
    public async Task WhenIReadThePaymentHistory() =>
        _history = await FolioQueries.Handle(new GetPaymentHistoryQuery(HotelId, Now.AddDays(-1), ArrivalAt.AddDays(5)));

    [When("proceso su pago desde el mismo módulo")]
    public async Task WhenTheFrontDeskChargesAndChecksOut()
    {
        (_folio, _receipts) = await Folios.Handle(new PayFolioCommand(_bookingId,
            [new FolioPaymentPart(48.50m, PaymentMethod.CardAtFrontDesk, "POS-1")], FrontDesk));
        await CheckOut(FrontDeskRequester);
    }

    // ── Then ────────────────────────────────────────────────────────

    [Then("el sistema me muestra el detalle de los cargos antes de permitirme finalizar")]
    public void ThenTheChargesAreShown()
    {
        var pending = _error.Should().BeOfType<FolioBalancePendingException>().Subject;
        pending.Code.Should().Be(FolioErrorCodes.BalancePending);
        pending.Balance.Should().Be(48.50m);
        pending.Parameters.Should().ContainKey("charges");
        StoredBooking().Status.Should().Be(BookingStatus.CheckedIn, "the guest cannot leave with charges unpaid");
    }

    [Then("recibo un comprobante digital de mi estancia por correo")]
    public void ThenIGetTheStatementOfTheStay()
    {
        StoredBooking().Status.Should().Be(BookingStatus.Completed);
        var statement = _notifications.Statements.Should().ContainSingle().Subject;
        statement.Folio.Status.Should().Be(FolioStatus.Closed);
        statement.Receipts.Should().NotBeEmpty("the stay lists its receipts");
    }

    [Then("el monto queda retenido y visible en mi folio de huésped")]
    public async Task ThenTheDepositIsVisible()
    {
        var folio = (await FolioQueries.Handle(new GetFolioByBookingIdQuery(_bookingId, Guest)))!;
        folio.DepositStatus.Should().Be(DepositStatus.Held);
        folio.DepositAmount.Should().Be(200m);
        folio.Balance.Should().Be(0, "the deposit is a guarantee, not a charge");
    }

    [Then("veo el detalle de todos los cargos antes de pagar")]
    public void ThenISeeEveryCharge()
    {
        _folio!.Charges.Select(charge => (charge.Description, charge.Amount)).Should().Equal(
            ("Minibar: 2 aguas y 1 snack", 18.50m), ("Lavandería (3 prendas)", 30m));
        _folio.Balance.Should().Be(48.50m);
    }

    [Then("el sistema lo procesa y actualiza mi folio a {string}")]
    public void ThenTheFolioIs(string status)
    {
        Stored().Status.Should().Be(FolioStatuses[status]);
        Stored().Balance.Should().Be(0);
        _receipts.Should().ContainSingle();
    }

    [Then("el sistema procesa ambos pagos correctamente")]
    public void ThenBothPaymentsAreProcessed()
    {
        Stored().Payments.Select(payment => (payment.Method, payment.Amount)).Should().Equal(
            (PaymentMethod.Yape, 70m), (PaymentMethod.Cash, 50m));
        Stored().Status.Should().Be(FolioStatus.Settled);
        _receipts.Select(receipt => receipt.Total).Should().Equal(70m, 50m);
    }

    [Then("el sistema inicia automáticamente la devolución del depósito")]
    public void ThenTheDepositIsReleased()
    {
        var folio = Stored();
        folio.Status.Should().Be(FolioStatus.Closed);
        folio.DepositStatus.Should().Be(DepositStatus.Released);
        folio.DepositReleasedAt.Should().Be(_clock.Now);
    }

    [Then("el sistema genera automáticamente el comprobante correspondiente")]
    public void ThenTheReceiptIsIssued()
    {
        var receipt = _context.Set<Receipt>().AsNoTracking().Should().ContainSingle().Subject;
        receipt.Source.Should().Be(ReceiptSource.BookingPayment);
        receipt.Type.Should().Be(ReceiptType.Boleta);
        receipt.FullNumber.Should().Be("B001-00000001");
        receipt.Total.Should().Be(StoredBooking().TotalPrice);
        (receipt.Subtotal + receipt.Igv).Should().Be(receipt.Total);
        _notifications.Sent.Should().ContainSingle(sent => sent.Id == receipt.Id);
    }

    [Then("el sistema genera el documento con esa información")]
    public void ThenTheInvoiceHasTheData()
    {
        var invoice = _receipts.Should().ContainSingle().Subject;
        invoice.Type.Should().Be(ReceiptType.Factura);
        invoice.FullNumber.Should().StartWith("F001-");
        invoice.CustomerDocument.Should().Be("20123456789");
        invoice.CustomerName.Should().Be("Inversiones Andinas S.A.C.");
    }

    [Then("le llega nuevamente a su correo registrado")]
    public void ThenItIsSentAgain()
    {
        var receipt = _receipts.Single();
        receipt.SentCount.Should().Be(2);
        _notifications.Sent.Should().ContainSingle().Which.CustomerEmail.Should().Be(StoredBooking().GuestEmail);
    }

    [Then("veo todos los cobros, reembolsos y comprobantes emitidos por fecha")]
    public void ThenTheHistoryHasEverything()
    {
        _history.Select(entry => entry.Type).Should().Contain(["BookingPayment", "Refund", "FolioPayment", "DepositHeld",
            "DepositReleased", "Receipt"]);
        _history.Select(entry => entry.OccurredAt).Should().BeInDescendingOrder();
        _history.Where(entry => entry.Type is "Refund" or "DepositReleased").Should().OnlyContain(entry => entry.Amount < 0);
    }

    [Then("su folio queda saldado y la habitación pasa a {string}")]
    public void ThenTheFolioIsSettledAndTheRoomIs(string status)
    {
        _folio!.Balance.Should().Be(0);
        Stored().Status.Should().Be(FolioStatus.Closed, "the check-out closed the settled folio");
        StoredBooking().Status.Should().Be(BookingStatus.Completed);
        _accommodations.Rooms[RoomId].Should().Be(status == "pendiente de limpieza" ? "Cleaning" : status);
    }

    // ── Support ─────────────────────────────────────────────────────

    /// <summary>A paid booking of the guest, checked in today in <see cref="RoomId"/>.</summary>
    private void AGuestStaying(bool paid = false)
    {
        var booking = PlaceForGuest(roomId: RoomId);
        _bookingId = Save(booking);
        booking.Confirm(Now);
        booking.CheckIn(ArrivalDay, ArrivalAt);
        booking.ClearDomainEvents();
        _context.SaveChanges();
        _accommodations.Rooms[RoomId] = "Occupied";
        if (paid) _clock.Now = ArrivalAt.AddDays(1);
    }

    private Task Charge(string description, FolioChargeCategory category, decimal amount) =>
        Folios.Handle(new PostFolioChargeCommand(_bookingId, description, category, amount, FrontDesk));

    /// <summary>What the payment use case does once the gateway approves it (see <c>PaymentCommandService</c>).</summary>
    private async Task PayTheBookingAsync()
    {
        var booking = _context.Set<Booking>().Single(b => b.Id == _bookingId);
        var payment = Payment.Register(booking.Id, booking.TotalPrice, PaymentMethod.Yape, "OP-123", null, FrontDeskUserId, Now);
        payment.Complete("TX-1", Now);
        _context.Add(payment);
        booking.Confirm(Now);
        await _services.GetRequiredService<IUnitOfWork>().CompleteAsync();
    }

    private Task CheckOutAsGuest() => CheckOut(GuestRequester);

    private async Task CheckOut(BookingRequester requester)
    {
        var service = new CheckInService(new BookingRepository(_context), new DigitalCheckInRepository(_context), null!,
            _accommodations, new NoGuestProfiles(), null!, null!,
            new HotelCalendar(Options.Create(new BookingPolicySettings()), _clock), _services.GetRequiredService<IUnitOfWork>(),
            new StayFolioGate(_services.GetRequiredService<IGuestFolioRepository>()), Options.Create(new BookingPolicySettings()));
        await service.Handle(new CheckOutCommand(_bookingId, requester));
    }

    /// <summary>Stores a new booking as its creation use case would (its creation event already published).</summary>
    private int Save(Booking booking)
    {
        _context.Add(booking);
        _context.SaveChanges();
        _ = booking.DomainEvents;
        booking.ClearDomainEvents();
        return booking.Id;
    }

    private Booking StoredBooking() => _context.Set<Booking>().AsNoTracking().Single(b => b.Id == _bookingId);

    private GuestFolio Stored() => _context.Set<GuestFolio>().AsNoTracking().Include(f => f.Charges).Include(f => f.Payments)
        .Single(f => f.BookingId == _bookingId);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingReceiptNotifications : IReceiptNotificationService
    {
        public List<Receipt> Sent { get; } = [];
        public List<(GuestFolio Folio, IReadOnlyList<Receipt> Receipts)> Statements { get; } = [];

        public Task SendReceiptAsync(Receipt receipt)
        {
            Sent.Add(receipt);
            return Task.CompletedTask;
        }

        public Task SendFolioStatementAsync(GuestFolio folio, IReadOnlyList<Receipt> receipts)
        {
            Statements.Add((folio, receipts));
            return Task.CompletedTask;
        }
    }

    /// <summary>The Bookings ACL over the same database (guests only see their own bookings).</summary>
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

        public Task<bool> ConfirmBookingAsync(int bookingId) => throw new NotSupportedException();

        public async Task<IReadOnlyList<int>> FetchBookingIdsOfHotelAsync(int hotelId) =>
            await context.Set<Booking>().Where(b => b.HotelId == hotelId).Select(b => b.Id).ToListAsync();
    }

    private sealed class NoGuestProfiles : IGuestProfilesContextFacade
    {
        public Task<Guid?> CreateGuestProfileAsync(string firstName, string lastName, string phone, string? email = null, int? userId = null) =>
            Task.FromResult<Guid?>(null);
        public Task<Guid?> FetchGuestProfileIdByUserIdAsync(int userId) => Task.FromResult<Guid?>(null);
        public Task<Guid?> FetchGuestProfileIdByEmailAsync(string email) => Task.FromResult<Guid?>(null);
        public Task<bool> LinkGuestProfileToUserAsync(Guid guestProfileId, int userId, string email) => Task.FromResult(false);
    }

    /// <summary>The rooms of the Accommodations service: a check-out turns an Occupied room into Cleaning.</summary>
    private sealed class FakeAccommodations : IAccommodationsContextFacade
    {
        public Dictionary<int, string> Rooms { get; } = [];

        public Task VacateRoomAfterCheckOutAsync(int roomId, int? checkedOutByUserId, string? checkedOutByEmail)
        {
            if (Rooms.GetValueOrDefault(roomId) == "Occupied") Rooms[roomId] = "Cleaning";
            return Task.CompletedTask;
        }

        public Task<RoomOffer?> FetchRoomAsync(int roomId) => throw new NotSupportedException();
        public Task OccupyRoomForCheckInAsync(int roomId, int? guestUserId, string? guestEmail) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, string>> FetchRoomNumbersAsync(IReadOnlyCollection<int> roomIds) => throw new NotSupportedException();
        public Task<HotelSummary?> FetchHotelAsync(int hotelId) => throw new NotSupportedException();
        public Task<HotelPaymentInstructions?> FetchPaymentInstructionsAsync(int hotelId) => throw new NotSupportedException();
        public Task<bool> HotelExistsAsync(int hotelId) => throw new NotSupportedException();
        public Task<bool> RoomExistsAsync(int roomId) => throw new NotSupportedException();
        public Task<decimal?> FetchRoomPricePerNightAsync(int roomId) => throw new NotSupportedException();
        public Task<int?> FetchHotelIdOfRoomAsync(int roomId) => throw new NotSupportedException();
        public Task<RoomOffer?> LockRoomForBookingAsync(int roomId) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoomOffer>> FetchRoomsOfferedForBookingAsync(int? hotelId) => throw new NotSupportedException();
    }
}
