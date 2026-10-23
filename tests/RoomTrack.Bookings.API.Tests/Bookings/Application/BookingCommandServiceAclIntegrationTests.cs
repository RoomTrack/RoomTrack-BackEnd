using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Bookings.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Bookings.Application.Internal.Configuration;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Commands;
using BackendAwRoomTrack.API.Bookings.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Bookings.Domain.Repositories;
using BackendAwRoomTrack.API.Bookings.Domain.Services;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Profiles.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using static BackendAwRoomTrack.API.Tests.Bookings.BookingTestData;

namespace BackendAwRoomTrack.API.Tests.Bookings.Application;

public class BookingCommandServiceAclIntegrationTests
{
    private const int StaffUserId = 7;

    private class FakeBookingRepository : IBookingRepository
    {
        public Booking? SavedBooking { get; private set; }
        public Booking? BookingToReturn { get; set; }

        public Task AddAsync(Booking entity)
        {
            SavedBooking = entity;
            return Task.CompletedTask;
        }

        public Task<Booking?> FindByIdAsync(int id) => Task.FromResult(BookingToReturn);

        public void Update(Booking entity) => SavedBooking = entity;

        public void Remove(Booking entity) { }

        public Task<IEnumerable<Booking>> ListAsync() => Task.FromResult(Enumerable.Empty<Booking>());

        public Task<IEnumerable<Booking>> FindByOwnerAsync(int userId, Guid? guestProfileId) => Task.FromResult(Enumerable.Empty<Booking>());

        public Task<IEnumerable<Booking>> ListNewestFirstAsync(int? hotelId) => Task.FromResult(Enumerable.Empty<Booking>());

        public Task<IEnumerable<Booking>> FindByRoomIdAsync(int roomId) => Task.FromResult(Enumerable.Empty<Booking>());

        // Every room is free: availability itself is covered by the domain service, not by these tests.
        public Task<bool> ExistsActiveBookingOverlappingAsync(int roomId, DateRange dates, int? excludingBookingId = null) =>
            Task.FromResult(false);

        public Task<IReadOnlySet<int>> FindRoomIdsWithActiveBookingOverlappingAsync(IReadOnlyCollection<int> roomIds, DateRange dates) =>
            Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());

        public Task<IReadOnlyList<Booking>> ListActiveOverlappingAsync(int? hotelId, DateRange window) =>
            Task.FromResult<IReadOnlyList<Booking>>([]);

        public Task<IReadOnlyDictionary<int, int>> CountActiveByRoomAsync(IReadOnlyCollection<int> roomIds) =>
            Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());

        public Task<IReadOnlyList<Booking>> ListPendingPaymentDueAsync(DateTimeOffset now) =>
            Task.FromResult<IReadOnlyList<Booking>>([]);
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public bool CompleteCalled { get; private set; }

        public Task CompleteAsync()
        {
            CompleteCalled = true;
            return Task.CompletedTask;
        }

        public Task ExecuteInTransactionAsync(Func<Task> work) => work();
    }

    private class FakeGuestProfilesContextFacade : IGuestProfilesContextFacade
    {
        public Func<int, Task<Guid?>>? FetchByUserIdHandler { get; set; }
        public Func<string, Task<Guid?>>? FetchByEmailHandler { get; set; }

        public int? LastCheckedUserId { get; private set; }
        public string? LastCheckedEmail { get; private set; }
        public bool FetchByUserIdCalled { get; private set; }
        public bool FetchByEmailCalled { get; private set; }

        public Task<Guid?> FetchGuestProfileIdByUserIdAsync(int userId)
        {
            FetchByUserIdCalled = true;
            LastCheckedUserId = userId;
            return FetchByUserIdHandler != null ? FetchByUserIdHandler(userId) : Task.FromResult<Guid?>(null);
        }

        public Task<Guid?> FetchGuestProfileIdByEmailAsync(string email)
        {
            FetchByEmailCalled = true;
            LastCheckedEmail = email;
            return FetchByEmailHandler != null ? FetchByEmailHandler(email) : Task.FromResult<Guid?>(null);
        }

        public Task<Guid?> CreateGuestProfileAsync(string firstName, string lastName, string phone, string? email = null, int? userId = null)
            => Task.FromResult<Guid?>(null);

        public Task<bool> LinkGuestProfileToUserAsync(Guid guestProfileId, int userId, string email)
            => Task.FromResult(true);
    }

    /// <summary>Accommodations port: every room of <see cref="BookingTestData.HotelId"/> exists and accepts bookings.</summary>
    private class FakeAccommodationsContextFacade : IAccommodationsContextFacade
    {
        public Task<RoomOffer?> LockRoomForBookingAsync(int roomId) => Task.FromResult<RoomOffer?>(Room(roomId));

        public Task<RoomOffer?> FetchRoomAsync(int roomId) => Task.FromResult<RoomOffer?>(Room(roomId));

        public Task<IReadOnlyDictionary<int, string>> FetchRoomNumbersAsync(IReadOnlyCollection<int> roomIds) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(roomIds.ToDictionary(id => id, id => id.ToString()));

        public Task<HotelSummary?> FetchHotelAsync(int hotelId) => Task.FromResult<HotelSummary?>(null);

        public Task<HotelPaymentInstructions?> FetchPaymentInstructionsAsync(int hotelId) =>
            Task.FromResult<HotelPaymentInstructions?>(null);

        public Task<bool> HotelExistsAsync(int hotelId) => Task.FromResult(hotelId == HotelId);

        public Task<bool> RoomExistsAsync(int roomId) => Task.FromResult(true);

        public Task<decimal?> FetchRoomPricePerNightAsync(int roomId) => Task.FromResult<decimal?>(Room(roomId).PricePerNight);

        public Task<int?> FetchHotelIdOfRoomAsync(int roomId) => Task.FromResult<int?>(HotelId);

        public Task OccupyRoomForCheckInAsync(int roomId, int? guestUserId, string? guestEmail) => Task.CompletedTask;

        public Task<IReadOnlyList<RoomOffer>> FetchRoomsOfferedForBookingAsync(int? hotelId) =>
            Task.FromResult<IReadOnlyList<RoomOffer>>([]);
    }

    /// <summary>IAM port: every user id is an active account with a predictable e-mail.</summary>
    private class FakeIamContextFacade : IIamContextFacade
    {
        public static string EmailOf(int userId) => $"user{userId}@example.com";

        public Task<UserContact?> FetchUserContactAsync(int userId) =>
            Task.FromResult<UserContact?>(new UserContact(userId, EmailOf(userId), $"User {userId}", "guest"));

        public Task<IReadOnlyList<UserContact>> ListHotelStaffAsync(int hotelId, IReadOnlyCollection<string> roles) =>
            Task.FromResult<IReadOnlyList<UserContact>>([]);

        public Task<int> FetchUserIdByEmail(string email) => Task.FromResult(0);

        public Task<string> FetchEmailByUserId(int userId) => Task.FromResult(EmailOf(userId));

        public Task<ReissuedSession?> AssignHotelToAdministratorAsync(int userId, int hotelId, SessionContext currentSession) =>
            Task.FromResult<ReissuedSession?>(null);
    }

    private readonly FakeBookingRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly HotelCalendar _calendar;
    private readonly IOptions<BookingPolicySettings> _settings = Options.Create(new BookingPolicySettings());

    public BookingCommandServiceAclIntegrationTests()
    {
        _calendar = new HotelCalendar(_settings, TimeProvider.System);
    }

    private BookingCommandService CreateService(FakeGuestProfilesContextFacade facade) =>
        new(_repo, _uow, facade, new FakeAccommodationsContextFacade(), new FakeIamContextFacade(),
            new RoomAvailabilityService(_repo), _calendar, _settings, NullLogger<BookingCommandService>.Instance);

    private static BookingRequester Staff => BookingRequester.HotelStaff(StaffUserId, HotelId);

    private CreateBookingCommand StaffBooking(string guestName, string? guestEmail, int? userId = null,
        Guid? guestProfileId = null, int roomId = 101)
    {
        var dates = Stay(_calendar.Today);
        return new CreateBookingCommand(Staff, roomId, guestName, guestEmail, dates.CheckIn, dates.CheckOut,
            UserId: userId, GuestProfileId: guestProfileId);
    }

    /// <summary>A booking already in the repository, placed for the guest <see cref="BookingTestData.GuestUserId"/>.</summary>
    private Booking ExistingGuestBooking()
    {
        var booking = Booking.Place(BookingRequester.Guest(GuestUserId, "Alice"), Room(), Stay(_calendar.Today),
            Contact(), GuestUserId, guestProfileId: null, _calendar.Today, _calendar.Now, PaymentHold);
        _repo.BookingToReturn = booking;
        return booking;
    }

    [Fact]
    public async Task CreateBooking_WithUserId_ShouldResolveGuestProfileIdViaUserId()
    {
        // Arrange
        var expectedProfileId = Guid.NewGuid();
        const int userId = 42;
        var facade = new FakeGuestProfilesContextFacade
        {
            FetchByUserIdHandler = id => Task.FromResult<Guid?>(id == userId ? expectedProfileId : null)
        };
        var service = CreateService(facade);

        // Act
        var result = await service.Handle(StaffBooking("Alice Wonderland", "alice@example.com", userId: userId));

        // Assert
        result.Should().NotBeNull();
        result.GuestProfileId.Should().Be(expectedProfileId);
        result.GuestName.Should().Be("Alice Wonderland");
        result.GuestEmail.Should().Be("alice@example.com");
        facade.FetchByUserIdCalled.Should().BeTrue();
        facade.LastCheckedUserId.Should().Be(userId);
        facade.FetchByEmailCalled.Should().BeFalse();
        _repo.SavedBooking.Should().BeSameAs(result);
        _uow.CompleteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBooking_WithoutUserId_WithEmail_ShouldResolveGuestProfileIdViaEmail()
    {
        // Arrange
        var expectedProfileId = Guid.NewGuid();
        const string email = "bob@example.com";
        var facade = new FakeGuestProfilesContextFacade
        {
            FetchByEmailHandler = e => Task.FromResult<Guid?>(e == email ? expectedProfileId : null)
        };
        var service = CreateService(facade);

        // Act
        var result = await service.Handle(StaffBooking("Bob Builder", email, roomId: 202));

        // Assert
        result.Should().NotBeNull();
        result.GuestProfileId.Should().Be(expectedProfileId);
        result.GuestName.Should().Be("Bob Builder");
        result.GuestEmail.Should().Be(email);
        facade.FetchByUserIdCalled.Should().BeFalse();
        facade.FetchByEmailCalled.Should().BeTrue();
        facade.LastCheckedEmail.Should().Be(email);
        _uow.CompleteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBooking_WhenGuestNotFoundInProfiles_ShouldCreateBookingWithNullGuestProfileId()
    {
        // Arrange
        var facade = new FakeGuestProfilesContextFacade
        {
            FetchByUserIdHandler = _ => Task.FromResult<Guid?>(null),
            FetchByEmailHandler = _ => Task.FromResult<Guid?>(null)
        };
        var service = CreateService(facade);

        // Act
        var result = await service.Handle(StaffBooking("Anonymous Guest", "anonymous@guest.com", userId: 999));

        // Assert
        result.Should().NotBeNull();
        result.GuestProfileId.Should().BeNull();
        result.GuestName.Should().Be("Anonymous Guest");
        result.GuestEmail.Should().Be("anonymous@guest.com");
        facade.FetchByUserIdCalled.Should().BeTrue();
        _uow.CompleteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBooking_WithExplicitGuestProfileId_ShouldNotQueryFacade()
    {
        // Arrange
        var explicitProfileId = Guid.NewGuid();
        var facade = new FakeGuestProfilesContextFacade();
        var service = CreateService(facade);

        // Act
        var result = await service.Handle(StaffBooking("Direct Profile Guest", "direct@example.com", userId: 50,
            guestProfileId: explicitProfileId));

        // Assert
        result.Should().NotBeNull();
        result.GuestProfileId.Should().Be(explicitProfileId);
        facade.FetchByUserIdCalled.Should().BeFalse();
        facade.FetchByEmailCalled.Should().BeFalse();
        _uow.CompleteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBooking_ByGuest_ShouldUseTheirGuestProfileAndAccountEmail()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var facade = new FakeGuestProfilesContextFacade
        {
            FetchByUserIdHandler = id => Task.FromResult<Guid?>(id == GuestUserId ? profileId : null)
        };
        var service = CreateService(facade);
        var dates = Stay(_calendar.Today);
        // The e-mail typed in the request is ignored: a guest's booking e-mails go to their own account.
        var command = new CreateBookingCommand(BookingRequester.Guest(GuestUserId, "Alice"), 101, "Alice Wonderland",
            "other@example.com", dates.CheckIn, dates.CheckOut);

        // Act
        var result = await service.Handle(command);

        // Assert
        result.GuestProfileId.Should().Be(profileId);
        result.GuestId!.Value.Should().Be(GuestUserId);
        result.GuestEmail.Should().Be(FakeIamContextFacade.EmailOf(GuestUserId));
        facade.LastCheckedUserId.Should().Be(GuestUserId);
        facade.FetchByEmailCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmBooking_ShouldUpdateBookingStatusToConfirmed()
    {
        // Arrange
        var booking = ExistingGuestBooking();
        var service = CreateService(new FakeGuestProfilesContextFacade());

        // Act
        var result = await service.Handle(new ConfirmBookingCommand(booking.Id));

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(BookingStatus.Confirmed);
        _uow.CompleteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CancelBooking_ShouldUpdateBookingStatusToCancelled()
    {
        // Arrange
        var booking = ExistingGuestBooking();
        var service = CreateService(new FakeGuestProfilesContextFacade());

        // Act
        var result = await service.Handle(new CancelBookingCommand(booking.Id, BookingRequester.Guest(GuestUserId, "Alice")));

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(BookingStatus.Cancelled);
        _uow.CompleteCalled.Should().BeTrue();
    }
}
