using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reqnroll;
using RoomTrack.Accommodations.Interfaces.ACL;
using RoomTrack.Bookings.API.Infrastructure.Persistence;
using RoomTrack.Bookings.Application.Internal.CommandServices;
using RoomTrack.Bookings.Application.Internal.Configuration;
using RoomTrack.Bookings.Application.OutboundServices;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.Events;
using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Bookings.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Payments.Application.ACL;
using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Profiles.Interfaces.ACL;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Repositories;
using static RoomTrack.Tests.Bookings.BookingTestData;

namespace RoomTrack.Tests.Features.Steps;

/// <summary>
///     Steps of the Sprint 2 stay scenarios (US-13, US-14, US-15, US-27) through the check-in/check-out use case
///     (<see cref="CheckInService"/>) and the Bookings domain, on an in-memory database. The rooms live in the
///     Accommodations service: <see cref="FakeAccommodations"/> follows its ACL contract (a check-in needs an
///     Available room and makes it Occupied; a check-out turns an Occupied room into Cleaning).
/// </summary>
/// <remarks>Each request runs with its own database context, as in the deployed service.</remarks>
[Binding]
public sealed class StaySteps : IDisposable
{
    private const int RoomId = 101;
    private static readonly BookingRequester Guest = BookingRequester.Guest(GuestUserId, "Alice");
    private static readonly BookingRequester FrontDesk = BookingRequester.HotelStaff(userId: 7, HotelId);

    /// <summary>The check-in day of the stay of <see cref="PlaceForGuest"/>, 15:00 in Lima.</summary>
    private static readonly DateTime ArrivalDay = Stay(Today).CheckIn;
    private static readonly DateTimeOffset ArrivalAt = new(ArrivalDay.Year, ArrivalDay.Month, ArrivalDay.Day, 20, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, string> RoomStatuses = new()
    {
        ["disponible"] = "Available",
        ["ocupada"] = "Occupied",
        ["pendiente de limpieza"] = "Cleaning"
    };

    private readonly string _database = Guid.NewGuid().ToString();
    private readonly List<BookingsDbContext> _contexts = [];
    private readonly FakeAccommodations _accommodations = new();
    private readonly FixedClock _clock = new(ArrivalAt);
    /// <summary>The context of the last request, whose tracked booking holds the events it raised.</summary>
    private BookingsDbContext _requestContext = null!;
    private int _bookingId;
    private Payment? _payment;
    private DomainException? _error;
    private IReadOnlyList<AvailableRoom>? _alternatives;

    public void Dispose() => _contexts.ForEach(context => context.Dispose());

    // ── Given ───────────────────────────────────────────────────────

    [Given("que tengo un problema durante el check-in digital")]
    public void GivenAProblemDuringTheCheckIn() => AConfirmedBookingInARoom("Available");

    [Given("que finalizo mi estancia sin cargos pendientes")]
    public async Task GivenMyStayEndsWithoutPendingCharges()
    {
        AConfirmedBookingInARoom("Available");
        await CheckIn(Guest);
        _clock.Now = ArrivalAt.AddDays(2);
    }

    [Given("que el pago se procesa correctamente")]
    public void GivenThePaymentIsProcessed()
    {
        _bookingId = Save(PlaceForGuest(roomId: RoomId));
        _accommodations.Rooms[RoomId] = "Available";
    }

    [Given("que un huésped llega sin haber hecho check-in digital")]
    public void GivenAGuestArrivesWithoutDigitalCheckIn() => AConfirmedBookingInARoom("Available");

    [Given("que un huésped llega antes de que su habitación esté lista")]
    public void GivenAGuestArrivesBeforeTheRoomIsReady()
    {
        AConfirmedBookingInARoom("Cleaning");
        _accommodations.Rooms[102] = "Available";
        _accommodations.Rooms[103] = "Cleaning";
    }

    // ── When ────────────────────────────────────────────────────────

    [When("solicito ayuda")]
    public Task WhenIAskForHelp() =>
        Service().Handle(new RequestCheckInAssistanceCommand(_bookingId, Guest, "No puedo subir mi documento"));

    [When("confirmo el check-out desde la app")]
    public Task WhenICheckOutFromTheApp() => Service().Handle(new CheckOutCommand(_bookingId, Guest));

    [When("finaliza")]
    public async Task WhenThePaymentEnds()
    {
        // What the payment use case does once the gateway approves it: the payment is completed and the booking
        // confirmed in the same transaction.
        var context = _requestContext = NewContext();
        var booking = (await new BookingRepository(context).FindByIdAsync(_bookingId))!;
        _payment = Payment.Register(booking.Id, booking.TotalPrice, PaymentMethod.Yape, "OP-123", null, recordedByUserId: 7, Now);
        _payment.Complete("TX-1", Now);
        booking.Confirm(Now);
        await context.SaveChangesAsync();
    }

    [When("registro sus datos y confirmo la reserva")]
    public Task WhenTheFrontDeskChecksTheGuestIn() => CheckIn(FrontDesk);

    [When("intento hacer check-in")]
    public async Task WhenTheFrontDeskTriesToCheckIn()
    {
        try { await CheckIn(FrontDesk); }
        catch (DomainException e) { _error = e; }
        if (_error is not null) _alternatives = await Service().FindAlternativeRoomsAsync(_bookingId, FrontDesk);
    }

    // ── Then ────────────────────────────────────────────────────────

    [Then("puedo contactar directamente a recepción desde la misma pantalla")]
    public void ThenTheFrontDeskIsContacted()
    {
        var request = _requestContext.ChangeTracker.Entries<Booking>().Single().Entity.DomainEvents
            .OfType<CheckInAssistanceRequestedEvent>().Should().ContainSingle().Subject;
        request.HotelId.Should().Be(HotelId);
        request.RoomId.Should().Be(RoomId);
        request.Message.Should().Be("No puedo subir mi documento");
    }

    [Then("mi habitación se libera y pasa a estado {string}")]
    public void ThenMyRoomIsReleased(string status)
    {
        var booking = StoredBooking();
        booking.Status.Should().Be(BookingStatus.Completed);
        booking.Status.IsActive().Should().BeFalse("a completed stay no longer holds the room");
        _accommodations.Rooms[RoomId].Should().Be(RoomStatuses[status]);
    }

    [Then("recibo una confirmación y mi check-in queda habilitado para completarse")]
    public void ThenTheBookingIsConfirmedAndCheckInIsEnabled()
    {
        _payment!.Status.Should().Be(PaymentStatus.Completed);
        var booking = StoredBooking();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        // The confirmation e-mail is sent by the handler of this event.
        _requestContext.ChangeTracker.Entries<Booking>().Single().Entity.DomainEvents
            .OfType<BookingConfirmedEvent>().Should().ContainSingle();
        var checkIn = () => booking.EnsureCheckInAllowed(ArrivalDay);
        checkIn.Should().NotThrow();
    }

    [Then("la habitación pasa a estado {string}")]
    public void ThenTheRoomIs(string status)
    {
        StoredBooking().Status.Should().Be(BookingStatus.CheckedIn);
        _accommodations.Rooms[RoomId].Should().Be(RoomStatuses[status]);
    }

    [Then("el sistema me avisa y sugiere habitaciones alternativas disponibles")]
    public void ThenTheSystemWarnsAndSuggestsAlternatives()
    {
        _error.Should().NotBeNull("the room is not ready");
        StoredBooking().Status.Should().Be(BookingStatus.Confirmed, "the refused check-in leaves nothing half-done");
        _alternatives!.Select(room => room.Room.RoomId).Should().Equal(102);
    }

    // ── Support ─────────────────────────────────────────────────────

    /// <summary>A paid booking of a guest for <see cref="RoomId"/>, which is in <paramref name="roomStatus"/>.</summary>
    private void AConfirmedBookingInARoom(string roomStatus)
    {
        var booking = PlaceForGuest(roomId: RoomId);
        booking.Confirm(Now);
        _bookingId = Save(booking);
        _accommodations.Rooms[RoomId] = roomStatus;
    }

    private Task CheckIn(BookingRequester requester) =>
        Service().Handle(new CompleteDigitalCheckInCommand(_bookingId, requester, IdentityDocumentType.Dni, "45678912", "PE",
            [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]));

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

    /// <summary>The use case for one request, with its own context.</summary>
    private CheckInService Service()
    {
        var context = _requestContext = NewContext();
        return new CheckInService(new BookingRepository(context), new DigitalCheckInRepository(context), new FakeDocumentStorage(),
            _accommodations, new NoGuestProfiles(), new FakeAvailability(_accommodations), new PlainSecretProtector(),
            new HotelCalendar(Options.Create(new BookingPolicySettings()), _clock), new ContextUnitOfWork(context),
            new StayFolioGate(new GuestFolioRepository(context)));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Saves on <c>CompleteAsync</c>; the in-memory provider has no transactions.</summary>
    private sealed class ContextUnitOfWork(BookingsDbContext context) : IUnitOfWork
    {
        public Task CompleteAsync() => context.SaveChangesAsync();
        public Task ExecuteInTransactionAsync(Func<Task> work) => work();
    }

    private sealed class FakeDocumentStorage : IDocumentStorage
    {
        public Task<string> SaveAsync(byte[] content, string contentType, CancellationToken cancellationToken = default) =>
            Task.FromResult($"documents/{Guid.NewGuid()}");
    }

    private sealed class PlainSecretProtector : ISecretProtector
    {
        public string Protect(string purpose, string plaintext) => plaintext;
        public string Unprotect(string purpose, string protectedValue) => protectedValue;
        public byte[] Protect(string purpose, byte[] plaintext) => plaintext;
        public byte[] Unprotect(string purpose, byte[] protectedValue) => protectedValue;
    }

    private sealed class NoGuestProfiles : IGuestProfilesContextFacade
    {
        public Task<Guid?> CreateGuestProfileAsync(string firstName, string lastName, string phone, string? email = null, int? userId = null) =>
            Task.FromResult<Guid?>(null);
        public Task<Guid?> FetchGuestProfileIdByUserIdAsync(int userId) => Task.FromResult<Guid?>(null);
        public Task<Guid?> FetchGuestProfileIdByEmailAsync(string email) => Task.FromResult<Guid?>(null);
        public Task<bool> LinkGuestProfileToUserAsync(Guid guestProfileId, int userId, string email) => Task.FromResult(false);
    }

    /// <summary>Every room of the hotel is free for the stay; whether it is ready is the room's status.</summary>
    private sealed class FakeAvailability(FakeAccommodations accommodations) : IRoomAvailabilityQueryService
    {
        public Task<IReadOnlyList<AvailableRoom>> Handle(GetAvailableRoomsQuery query) =>
            Task.FromResult<IReadOnlyList<AvailableRoom>>(accommodations.Rooms.Keys
                .Select(id => new AvailableRoom(accommodations.Offer(id), query.Dates)).ToList());
    }

    /// <summary>The rooms of the Accommodations service, following its ACL contract.</summary>
    private sealed class FakeAccommodations : IAccommodationsContextFacade
    {
        public Dictionary<int, string> Rooms { get; } = [];

        public RoomOffer Offer(int roomId) => Room(roomId) with { Status = Rooms[roomId] };

        public Task<RoomOffer?> FetchRoomAsync(int roomId) =>
            Task.FromResult(Rooms.ContainsKey(roomId) ? Offer(roomId) : null);

        public Task OccupyRoomForCheckInAsync(int roomId, int? guestUserId, string? guestEmail)
        {
            if (Rooms[roomId] != "Available")
                throw new BusinessRuleViolationException("room.not_ready", $"Room {roomId} is not ready yet ({Rooms[roomId]}).");
            Rooms[roomId] = "Occupied";
            return Task.CompletedTask;
        }

        public Task VacateRoomAfterCheckOutAsync(int roomId, int? checkedOutByUserId, string? checkedOutByEmail)
        {
            if (Rooms[roomId] == "Occupied") Rooms[roomId] = "Cleaning";
            return Task.CompletedTask;
        }

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
