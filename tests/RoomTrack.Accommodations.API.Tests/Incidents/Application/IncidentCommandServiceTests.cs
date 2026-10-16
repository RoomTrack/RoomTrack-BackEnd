using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BackendAwRoomTrack.API.Accommodations.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Accommodations.Application.Internal.Configuration;
using BackendAwRoomTrack.API.Accommodations.Application.Internal.QueryServices;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Entities;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Accommodations.Infrastructure.Persistence.EFC.Repositories;
using BackendAwRoomTrack.API.IAM.Domain.Model.Constants;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Incidents.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Incidents.Application.Internal.Configuration;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Commands;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Events;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Incidents.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Incidents.Infrastructure.Persistence.EFC.Repositories;
using BackendAwRoomTrack.API.Shared.Application.OutboundServices;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Repositories;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Events;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoomTrack.Accommodations.API.Infrastructure.Persistence;
using Xunit;

namespace BackendAwRoomTrack.API.Tests.Incidents.Application;

/// <summary>
///     Incident use cases against the real Rooms context and an in-memory database: a blocking incident and the
///     status of its room change together.
/// </summary>
public class IncidentCommandServiceTests : IDisposable
{
    private static readonly IncidentActor Reception = new(10, "recepcion@roomtrack.pe", IsAdministrator: false);
    private static readonly IncidentActor Maintenance = new(20, "mantenimiento@roomtrack.pe", IsAdministrator: false);
    private static readonly IncidentActor Admin = new(1, "admin@roomtrack.pe", IsAdministrator: true);

    private readonly AccommodationsDbContext _context = new(new DbContextOptionsBuilder<AccommodationsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly RecordingDispatcher _dispatcher = new();
    private readonly FakeIam _iam = new();
    private readonly IncidentCommandService _service;
    private readonly RoomCommandService _rooms;
    private int _hotelId;
    private int _roomId;

    public IncidentCommandServiceTests()
    {
        var unitOfWork = new UnitOfWork(_context, _dispatcher);
        var roomRepository = new RoomRepository(_context);
        var statusChanges = new RoomStatusChangeRepository(_context);
        _rooms = new RoomCommandService(roomRepository, statusChanges, new RoomTypeRepository(_context), null!, unitOfWork,
            Options.Create(new RoomOperationsSettings()), TimeProvider.System, null!);
        _service = new IncidentCommandService(new IncidentRepository(_context),
            new RoomQueryService(roomRepository, statusChanges), _rooms, _iam, unitOfWork,
            Options.Create(new IncidentSettings()), TimeProvider.System);
        SeedRoom();
    }

    public void Dispose() => _context.Dispose();

    private void SeedRoom()
    {
        var hotel = new Hotel();
        var roomType = new RoomType("Standard", "Standard room");
        _context.AddRange(hotel, roomType);
        _context.SaveChanges();
        var room = new Room(new CreateRoomCommand(hotel.Id, roomType.Id, 120m, "Room 101", [], "101"));
        _context.Add(room);
        _context.SaveChanges();
        _hotelId = hotel.Id;
        _roomId = room.Id;
    }

    private Room Room() => _context.Set<Room>().Single(r => r.Id == _roomId);

    private Task<Incident> ReportAsync(bool blocksRoom, IncidentPriority priority = IncidentPriority.High) =>
        _service.Handle(new ReportIncidentCommand(_roomId, "Leaking shower", null, priority, blocksRoom, Reception));

    private async Task<Incident> ResolveAsync(Incident incident)
    {
        await _service.Handle(new AssignIncidentCommand(incident.Id, Maintenance.UserId, Maintenance));
        await _service.Handle(new StartIncidentCommand(incident.Id, Maintenance));
        return (await _service.Handle(new ResolveIncidentCommand(incident.Id, "Fixed.", Maintenance)))!;
    }

    [Fact]
    public async Task Report_ShouldPersistTheIncidentOfTheRoomsHotel_AndRaiseTheReportedEvent()
    {
        var incident = await ReportAsync(blocksRoom: false, IncidentPriority.Critical);

        incident.Id.Should().BePositive();
        incident.HotelId.Should().Be(_hotelId);
        _dispatcher.Events.OfType<IncidentReportedEvent>().Should().ContainSingle()
            .Which.IncidentId.Should().Be(incident.Id);
        Room().Status.Should().Be(RoomStatus.Available);
    }

    [Fact]
    public async Task Report_OfAnUnknownRoom_ShouldFail()
    {
        var act = () => _service.Handle(new ReportIncidentCommand(9999, "Broken door", null, IncidentPriority.Low, false, Reception));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task ABlockingIncident_ShouldKeepTheRoomOutOfService_UntilItIsResolved()
    {
        var incident = await ReportAsync(blocksRoom: true);

        Room().Status.Should().Be(RoomStatus.Maintenance);
        Room().IsBlockedByIncident.Should().BeTrue();
        var makeAvailable = () => _rooms.Handle(new ChangeRoomStatusCommand(_roomId, RoomStatus.Available, Admin.UserId, Admin.Email));
        await makeAvailable.Should().ThrowAsync<RoomBlockedByIncidentException>();

        await ResolveAsync(incident);

        Room().Status.Should().Be(RoomStatus.Cleaning);
        Room().IsBlockedByIncident.Should().BeFalse();
        _context.Set<RoomStatusChange>().Where(c => c.RoomId == _roomId).Select(c => c.Origin)
            .Should().OnlyContain(origin => origin == RoomStatusChangeOrigin.Incident);
    }

    [Fact]
    public async Task ResolvingOneOfTwoBlockingIncidents_ShouldKeepTheRoomBlocked()
    {
        var first = await ReportAsync(blocksRoom: true);
        var second = await ReportAsync(blocksRoom: true);

        await ResolveAsync(first);

        Room().Status.Should().Be(RoomStatus.Maintenance);
        Room().IsBlockedByIncident.Should().BeTrue();

        await ResolveAsync(second);

        Room().Status.Should().Be(RoomStatus.Cleaning);
    }

    [Fact]
    public async Task Assign_ToSomeoneWhoIsNotMaintenanceOfTheHotel_ShouldFail()
    {
        var incident = await ReportAsync(blocksRoom: false);

        var act = () => _service.Handle(new AssignIncidentCommand(incident.Id, AssigneeUserId: 99, Admin));

        (await act.Should().ThrowAsync<InvalidFieldException>()).Which.Violation.Code
            .Should().Be(IncidentErrorCodes.AssigneeNotMaintenance);
    }

    [Fact]
    public async Task Assign_ByAnAdministratorToMaintenanceOfTheHotel_ShouldSucceed()
    {
        _iam.Maintenance.Add(new UserContact(Maintenance.UserId, Maintenance.Email!, "Mario Mantenimiento", UserRoles.Maintenance));
        var incident = await ReportAsync(blocksRoom: false);

        var assigned = await _service.Handle(new AssignIncidentCommand(incident.Id, Maintenance.UserId, Admin));

        assigned!.AssigneeUserId.Should().Be(Maintenance.UserId);
    }

    [Fact]
    public async Task Start_AnUnassignedIncidentByMaintenance_ShouldTakeIt()
    {
        var incident = await ReportAsync(blocksRoom: false);

        var started = await _service.Handle(new StartIncidentCommand(incident.Id, Maintenance));

        started!.AssigneeUserId.Should().Be(Maintenance.UserId);
        started.Status.Should().Be(IncidentStatus.InProgress);
    }

    [Fact]
    public async Task EscalateLateIncidents_ShouldOnlyEscalateLateCriticalPendingOnes()
    {
        var critical = await ReportAsync(blocksRoom: false, IncidentPriority.Critical);
        await ReportAsync(blocksRoom: false, IncidentPriority.High);
        var service = new IncidentCommandService(new IncidentRepository(_context), null!, _rooms, _iam,
            new UnitOfWork(_context, _dispatcher), Options.Create(new IncidentSettings()),
            new FixedTimeProvider(critical.ReportedAt.AddMinutes(61)));

        (await service.Handle(new EscalateLateIncidentsCommand())).Should().Be(1);
        (await service.Handle(new EscalateLateIncidentsCommand())).Should().Be(0);
    }

    private sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        public List<IEvent> Events { get; } = [];

        public Task DispatchAsync(IEnumerable<IEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            Events.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIam : IIamContextFacade
    {
        public List<UserContact> Maintenance { get; } = [];

        public Task<UserContact?> FetchUserContactAsync(int userId) =>
            Task.FromResult(Maintenance.FirstOrDefault(contact => contact.UserId == userId));

        public Task<IReadOnlyList<UserContact>> ListHotelStaffAsync(int hotelId, IReadOnlyCollection<string> roles) =>
            Task.FromResult<IReadOnlyList<UserContact>>(roles.Contains(UserRoles.Maintenance) ? Maintenance : []);

        public Task<int> FetchUserIdByEmail(string email) => throw new NotSupportedException();
        public Task<string> FetchEmailByUserId(int userId) => throw new NotSupportedException();

        public Task<ReissuedSession?> AssignHotelToAdministratorAsync(int userId, int hotelId, SessionContext currentSession) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
