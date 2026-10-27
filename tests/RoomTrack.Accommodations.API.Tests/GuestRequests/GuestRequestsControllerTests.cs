using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using RoomTrack.Accommodations.Application.Internal.QueryServices;
using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Domain.Model.Entities;
using RoomTrack.Accommodations.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.GuestRequests.Application.Internal.CommandServices;
using RoomTrack.GuestRequests.Application.Internal.Configuration;
using RoomTrack.GuestRequests.Application.Internal.QueryServices;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Exceptions;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.GuestRequests.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.GuestRequests.Interfaces.REST;
using RoomTrack.GuestRequests.Interfaces.REST.Resources;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoomTrack.Accommodations.API.Infrastructure.Persistence;
using Xunit;

namespace RoomTrack.Tests.GuestRequests;

/// <summary>
///     Who may see and change which guest requests (US-11, US-19), through the controller against the real services and
///     an in-memory database: a guest only their own requests, hotel staff only those of their hotel.
/// </summary>
/// <remarks>The role policies of the endpoints are applied by ASP.NET Core, not here: these tests cover the checks of the controller and the aggregate.</remarks>
public class GuestRequestsControllerTests : IDisposable
{
    private const int GuestId = 50;
    private const int OtherGuestId = 51;
    private const int ReceptionId = 10;
    private const int OtherHotelReceptionId = 11;
    private const int HousekeeperId = 30;
    private const int MaintenanceId = 20;
    private const int ChainAdminId = 1;

    private readonly AccommodationsDbContext _context = new(new DbContextOptionsBuilder<AccommodationsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly FakeReservations _reservations = new();
    private readonly GuestRequestCommandService _commandService;
    private readonly GuestRequestQueryService _queryService;
    private readonly RoomQueryService _roomQueries;
    private int _hotelId;
    private int _otherHotelId;
    private int _roomId;
    private int _otherHotelRoomId;

    public GuestRequestsControllerTests()
    {
        var requests = new GuestRequestRepository(_context);
        _roomQueries = new RoomQueryService(new RoomRepository(_context), new RoomStatusChangeRepository(_context));
        _queryService = new GuestRequestQueryService(requests);
        _commandService = new GuestRequestCommandService(requests, _reservations, _roomQueries, new FakeIam(),
            new UnitOfWork(_context, new NoEvents()), Options.Create(new GuestRequestSettings()), TimeProvider.System);
        Seed();
    }

    public void Dispose() => _context.Dispose();

    private void Seed()
    {
        var hotel = new Hotel();
        var otherHotel = new Hotel();
        var roomType = new RoomType("Standard", "Standard room");
        _context.AddRange(hotel, otherHotel, roomType);
        _context.SaveChanges();
        var room = new Room(new CreateRoomCommand(hotel.Id, roomType.Id, 120m, "Room 101", [], "101"));
        var otherHotelRoom = new Room(new CreateRoomCommand(otherHotel.Id, roomType.Id, 120m, "Room 201", [], "201"));
        _context.AddRange(room, otherHotelRoom);
        _context.SaveChanges();
        _hotelId = hotel.Id;
        _otherHotelId = otherHotel.Id;
        _roomId = room.Id;
        _otherHotelRoomId = otherHotelRoom.Id;
        _reservations.Stays.Add(new GuestStay(7, "SS-ABCDEFGH", _hotelId, _roomId, GuestId, DateTime.Today, DateTime.Today.AddDays(2), true));
        _reservations.Stays.Add(new GuestStay(8, "SS-IJKLMNOP", _otherHotelId, _otherHotelRoomId, OtherGuestId, DateTime.Today,
            DateTime.Today.AddDays(2), true));
    }

    private GuestRequestsController AsGuest(int userId = GuestId) => Controller(userId, UserRoles.Guest, null);
    private GuestRequestsController AsReception() => Controller(ReceptionId, UserRoles.Reception, _hotelId);
    private GuestRequestsController AsOtherHotelReception() => Controller(OtherHotelReceptionId, UserRoles.Reception, _otherHotelId);
    private GuestRequestsController AsHousekeeper() => Controller(HousekeeperId, UserRoles.Housekeeping, _hotelId);
    private GuestRequestsController AsMaintenance() => Controller(MaintenanceId, UserRoles.Maintenance, _hotelId);
    private GuestRequestsController AsChainAdmin() => Controller(ChainAdminId, UserRoles.ChainAdmin, null);

    private GuestRequestsController Controller(int userId, string role, int? hotelId)
    {
        List<Claim> claims =
        [
            new(IamClaimTypes.UserId, userId.ToString()),
            new(IamClaimTypes.Username, $"user{userId}@roomtrack.pe"),
            new(IamClaimTypes.Role, role)
        ];
        if (hotelId is { } id) claims.Add(new Claim(IamClaimTypes.HotelId, id.ToString()));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", IamClaimTypes.Username, IamClaimTypes.Role));
        return new GuestRequestsController(_commandService, _queryService, _roomQueries)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
        };
    }

    /// <summary>A request the guest sent from the app for the room of their stay.</summary>
    private async Task<GuestRequestResource> SentByGuestAsync(int guestId = GuestId, ServiceArea? area = null)
    {
        var result = await AsGuest(guestId).CreateGuestRequest(new CreateGuestRequestResource("Toallas extra", null, area, null));
        return (GuestRequestResource)result.Should().BeOfType<CreatedAtActionResult>().Subject.Value!;
    }

    private GuestRequest Stored(int id) => _context.Set<GuestRequest>().AsNoTracking().Single(r => r.Id == id);

    [Fact]
    public async Task AGuest_ShouldSendTheRequestForTheRoomOfTheirStay()
    {
        var request = await SentByGuestAsync();

        request.HotelId.Should().Be(_hotelId);
        request.RoomId.Should().Be(_roomId);
        Stored(request.Id).GuestUserId.Should().Be(GuestId);
    }

    [Fact]
    public async Task TheStaffOfAnArea_ShouldNotCreateRequests()
    {
        var result = await AsHousekeeper().CreateGuestRequest(new CreateGuestRequestResource("Toallas", null, ServiceArea.Housekeeping, _roomId));

        result.Should().BeOfType<ForbidResult>();
        _context.Set<GuestRequest>().Should().BeEmpty();
    }

    [Fact]
    public async Task TheFrontDesk_ShouldNotRegisterARequestForARoomOfAnotherHotel()
    {
        var result = await AsOtherHotelReception().CreateGuestRequest(
            new CreateGuestRequestResource("Toallas", null, ServiceArea.Housekeeping, _roomId));

        result.Should().BeOfType<ForbidResult>();
        _context.Set<GuestRequest>().Should().BeEmpty();
    }

    [Fact]
    public async Task AGuest_ShouldListOnlyTheirOwnRequests()
    {
        var mine = await SentByGuestAsync();
        await SentByGuestAsync(OtherGuestId);

        var result = await AsGuest().GetGuestRequests(hotelId: _otherHotelId, area: null, status: null, roomId: null);

        result.Should().BeOfType<OkObjectResult>().Which.Value.As<IEnumerable<GuestRequestResource>>()
            .Should().ContainSingle().Which.Id.Should().Be(mine.Id);
    }

    [Fact]
    public async Task TheStaff_ShouldNotListTheRequestsOfAnotherHotel()
    {
        await SentByGuestAsync();

        var result = await AsOtherHotelReception().GetGuestRequests(hotelId: _hotelId, area: null, status: null, roomId: null);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task TheStaff_ShouldListTheRequestsOfTheirHotel_WhenNoHotelIsSent()
    {
        var ofMyHotel = await SentByGuestAsync();
        await SentByGuestAsync(OtherGuestId);

        var result = await AsReception().GetGuestRequests(hotelId: null, area: null, status: null, roomId: null);

        result.Should().BeOfType<OkObjectResult>().Which.Value.As<IEnumerable<GuestRequestResource>>()
            .Should().ContainSingle().Which.Id.Should().Be(ofMyHotel.Id);
    }

    [Fact]
    public async Task ARequest_ShouldBeHiddenFromAnotherGuest_AndFromTheStaffOfAnotherHotel()
    {
        var request = await SentByGuestAsync();

        (await AsGuest(OtherGuestId).GetGuestRequest(request.Id)).Should().BeOfType<NotFoundResult>();
        (await AsOtherHotelReception().GetGuestRequest(request.Id)).Should().BeOfType<NotFoundResult>();
        (await AsGuest().GetGuestRequest(request.Id)).Should().BeOfType<OkObjectResult>();
        (await AsHousekeeper().GetGuestRequest(request.Id)).Should().BeOfType<OkObjectResult>();
        (await AsChainAdmin().GetGuestRequest(request.Id)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TheStaffOfAnotherHotel_ShouldNotChangeARequest()
    {
        var request = await SentByGuestAsync();
        var otherHotel = AsOtherHotelReception();

        (await otherHotel.RouteGuestRequest(request.Id, new RouteGuestRequestResource(ServiceArea.Housekeeping)))
            .Should().BeOfType<NotFoundResult>();
        (await otherHotel.StartGuestRequest(request.Id)).Should().BeOfType<NotFoundResult>();
        (await otherHotel.ResolveGuestRequest(request.Id, null)).Should().BeOfType<NotFoundResult>();
        (await otherHotel.CancelGuestRequest(request.Id, null)).Should().BeOfType<NotFoundResult>();

        var stored = Stored(request.Id);
        stored.Area.Should().Be(ServiceArea.Reception);
        stored.Status.Should().Be(GuestRequestStatus.Pending);
    }

    [Fact]
    public async Task AGuest_ShouldWithdrawTheirPendingRequest_ButNotAnotherGuestsOne()
    {
        var request = await SentByGuestAsync();

        (await AsGuest(OtherGuestId).CancelGuestRequest(request.Id, null)).Should().BeOfType<NotFoundResult>();
        Stored(request.Id).Status.Should().Be(GuestRequestStatus.Pending);

        (await AsGuest().CancelGuestRequest(request.Id, null)).Should().BeOfType<OkObjectResult>();
        Stored(request.Id).Status.Should().Be(GuestRequestStatus.Cancelled);
    }

    [Fact]
    public async Task AGuest_ShouldNotWithdrawARequestTheAreaAlreadyStarted()
    {
        var request = await SentByGuestAsync();
        await AsReception().StartGuestRequest(request.Id);

        var act = () => AsGuest().CancelGuestRequest(request.Id, null);

        await act.Should().ThrowAsync<GuestRequestNotAllowedException>();
    }

    [Fact]
    public async Task TheStaffOfAnotherArea_ShouldNotAttendARequest()
    {
        var request = await SentByGuestAsync(area: ServiceArea.Housekeeping);

        var act = () => AsMaintenance().StartGuestRequest(request.Id);

        await act.Should().ThrowAsync<GuestRequestNotAllowedException>();
        Stored(request.Id).Status.Should().Be(GuestRequestStatus.Pending);
    }

    [Fact]
    public async Task OnlyTheFrontDesk_ShouldRoute()
    {
        var request = await SentByGuestAsync();

        var act = () => AsHousekeeper().RouteGuestRequest(request.Id, new RouteGuestRequestResource(ServiceArea.Housekeeping));

        await act.Should().ThrowAsync<GuestRequestNotAllowedException>();
        Stored(request.Id).Area.Should().Be(ServiceArea.Reception);
    }

    [Fact]
    public async Task TheStaffOfTheArea_ShouldTakeAnUnassignedRequest_ButNotGiveItToSomeoneElse()
    {
        var request = await SentByGuestAsync(area: ServiceArea.Housekeeping);

        var giveAway = () => AsHousekeeper().AssignGuestRequest(request.Id, new AssignGuestRequestResource(MaintenanceId));
        await giveAway.Should().ThrowAsync<InvalidFieldException>();

        (await AsHousekeeper().AssignGuestRequest(request.Id, new AssignGuestRequestResource(HousekeeperId)))
            .Should().BeOfType<OkObjectResult>();
        Stored(request.Id).AssigneeUserId.Should().Be(HousekeeperId);
    }

    [Fact]
    public async Task TheFrontDesk_ShouldNotAssignSomeoneOutsideTheArea()
    {
        var request = await SentByGuestAsync(area: ServiceArea.Housekeeping);

        var act = () => AsReception().AssignGuestRequest(request.Id, new AssignGuestRequestResource(MaintenanceId));

        (await act.Should().ThrowAsync<InvalidFieldException>()).Which.Violation.Code
            .Should().Be(GuestRequestErrorCodes.AssigneeNotOfArea);
        Stored(request.Id).AssigneeUserId.Should().BeNull();
    }

    private sealed class FakeReservations : IRoomReservationsFacade
    {
        public List<GuestStay> Stays { get; } = [];

        public Task<GuestStay?> FetchCurrentStayAsync(int guestUserId) =>
            Task.FromResult(Stays.FirstOrDefault(stay => stay.GuestUserId == guestUserId));

        public Task<GuestStay?> FetchCurrentStayOfRoomAsync(int roomId) =>
            Task.FromResult(Stays.FirstOrDefault(stay => stay.RoomId == roomId));

        public Task<IReadOnlyDictionary<int, int>> CountActiveBookingsAsync(IReadOnlyCollection<int> roomIds) =>
            throw new NotSupportedException();

        public Task<bool> HasCurrentConfirmedStayAsync(int guestUserId, int roomId, DateTime day) => throw new NotSupportedException();
    }

    private sealed class FakeIam : IIamContextFacade
    {
        private static readonly List<UserContact> Staff =
        [
            new(ReceptionId, "recepcion@roomtrack.pe", "Recepción", UserRoles.Reception),
            new(HousekeeperId, "ana@roomtrack.pe", "Ana", UserRoles.Housekeeping),
            new(MaintenanceId, "mario@roomtrack.pe", "Mario", UserRoles.Maintenance)
        ];

        public Task<IReadOnlyList<UserContact>> ListHotelStaffAsync(int hotelId, IReadOnlyCollection<string> roles) =>
            Task.FromResult<IReadOnlyList<UserContact>>(Staff.Where(contact => roles.Contains(contact.Role)).ToList());

        public Task<UserContact?> FetchUserContactAsync(int userId) =>
            Task.FromResult(Staff.FirstOrDefault(contact => contact.UserId == userId));

        public Task<int> FetchUserIdByEmail(string email) => throw new NotSupportedException();
        public Task<string> FetchEmailByUserId(int userId) => throw new NotSupportedException();

        public Task<ReissuedSession?> AssignHotelToAdministratorAsync(int userId, int hotelId, SessionContext currentSession) =>
            throw new NotSupportedException();
    }

    /// <summary>The notifications are not what these tests are about.</summary>
    private sealed class NoEvents : IDomainEventDispatcher
    {
        public Task DispatchAsync(IEnumerable<IEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
