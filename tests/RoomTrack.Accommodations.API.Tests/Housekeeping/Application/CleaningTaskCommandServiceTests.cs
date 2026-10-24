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
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Events;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Accommodations.Infrastructure.Persistence.EFC.Repositories;
using BackendAwRoomTrack.API.Housekeeping.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Housekeeping.Application.Internal.EventHandlers;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.Commands;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Housekeeping.Infrastructure.Persistence.EFC.Repositories;
using BackendAwRoomTrack.API.IAM.Domain.Model.Constants;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Incidents.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Incidents.Application.Internal.Configuration;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Commands;
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

namespace BackendAwRoomTrack.API.Tests.Housekeeping.Application;

/// <summary>
///     Cleaning task use cases against the real Rooms and Incidents contexts and an in-memory database: a task and the
///     status of its room change together, and rooms that go to Cleaning get their task.
/// </summary>
public class CleaningTaskCommandServiceTests : IDisposable
{
    private static readonly CleaningTaskActor Reception = new(10, "recepcion@roomtrack.pe", CleaningTaskRole.FrontDesk);
    private static readonly CleaningTaskActor Ana = new(30, "ana@roomtrack.pe", CleaningTaskRole.Housekeeper);
    private static readonly CleaningTaskActor Beto = new(31, "beto@roomtrack.pe", CleaningTaskRole.Housekeeper);
    private static readonly IncidentActor Maintenance = new(20, "mantenimiento@roomtrack.pe", IsAdministrator: false);

    private readonly AccommodationsDbContext _context = new(new DbContextOptionsBuilder<AccommodationsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly FakeIam _iam = new();
    private readonly CleaningTaskCommandService _service;
    private readonly IncidentCommandService _incidents;
    private readonly RoomCommandService _rooms;
    private int _hotelId;
    private int _roomId;
    private int _otherRoomId;

    public CleaningTaskCommandServiceTests()
    {
        var tasks = new CleaningTaskRepository(_context);
        var settings = new HotelHousekeepingSettingsRepository(_context);
        var strategy = new LeastWorkloadAssignmentStrategy(tasks, _iam);
        var dispatcher = new RoomEventsDispatcher(new RoomCleaningTaskHandler(tasks, settings, strategy, TimeProvider.System));
        var unitOfWork = new UnitOfWork(_context, dispatcher);
        var roomRepository = new RoomRepository(_context);
        var statusChanges = new RoomStatusChangeRepository(_context);
        var roomQueries = new RoomQueryService(roomRepository, statusChanges);
        _rooms = new RoomCommandService(roomRepository, statusChanges, new RoomTypeRepository(_context), null!, unitOfWork,
            Options.Create(new RoomOperationsSettings()), TimeProvider.System, null!);
        _incidents = new IncidentCommandService(new IncidentRepository(_context), roomQueries, _rooms, _iam, unitOfWork,
            Options.Create(new IncidentSettings()), TimeProvider.System);
        _service = new CleaningTaskCommandService(tasks, settings, strategy, roomQueries, _rooms, _incidents, _iam, unitOfWork,
            TimeProvider.System);
        Seed();
    }

    public void Dispose() => _context.Dispose();

    private void Seed()
    {
        var hotel = new Hotel();
        var roomType = new RoomType("Standard", "Standard room");
        _context.AddRange(hotel, roomType);
        _context.SaveChanges();
        var room = new Room(new CreateRoomCommand(hotel.Id, roomType.Id, 120m, "Room 101", [], "101"));
        var other = new Room(new CreateRoomCommand(hotel.Id, roomType.Id, 120m, "Room 102", [], "102"));
        _context.AddRange(room, other);
        _context.SaveChanges();
        _hotelId = hotel.Id;
        _roomId = room.Id;
        _otherRoomId = other.Id;
        _iam.Housekeeping.Add(new UserContact(Ana.UserId, Ana.Email!, "Ana Limpieza", UserRoles.Housekeeping));
        _iam.Housekeeping.Add(new UserContact(Beto.UserId, Beto.Email!, "Beto Limpieza", UserRoles.Housekeeping));
    }

    private Room Room(int? id = null) => _context.Set<Room>().Single(r => r.Id == (id ?? _roomId));

    private Task<CleaningTask> CreateAsync(int? assigneeUserId = null, int? roomId = null) =>
        _service.Handle(new CreateCleaningTaskCommand(roomId ?? _roomId, CleaningTaskPriority.Normal, null, assigneeUserId, Reception));

    private Task EnableAutoAssignAsync() => _service.Handle(new ConfigureHousekeepingCommand(_hotelId, true, 1));

    [Fact]
    public async Task Create_WithAnAssignee_ShouldPutTheTaskInTheirList()
    {
        var task = await CreateAsync(Ana.UserId);

        task.Id.Should().BePositive();
        task.HotelId.Should().Be(_hotelId);
        task.AssigneeUserId.Should().Be(Ana.UserId);
    }

    [Fact]
    public async Task Create_ForSomeoneWhoIsNotHousekeepingOfTheHotel_ShouldFail()
    {
        var act = () => CreateAsync(assigneeUserId: 99);

        (await act.Should().ThrowAsync<InvalidFieldException>()).Which.Violation.Code
            .Should().Be(CleaningTaskErrorCodes.AssigneeNotHousekeeping);
    }

    [Fact]
    public async Task Create_WithoutAssignee_ShouldStayUnassigned_UnlessTheHotelAssignsAutomatically()
    {
        (await CreateAsync()).AssigneeUserId.Should().BeNull();

        await EnableAutoAssignAsync();
        var automatic = await CreateAsync(roomId: _otherRoomId);

        automatic.AssigneeUserId.Should().NotBeNull();
        automatic.AssignedAutomatically.Should().BeTrue();
    }

    [Fact]
    public async Task AutomaticAssignment_ShouldChooseTheHousekeeperWithTheFewestOpenTasks()
    {
        // Ana (the lowest id, chosen on ties) already has one task.
        await CreateAsync(Ana.UserId);
        await EnableAutoAssignAsync();

        var task = await CreateAsync(roomId: _otherRoomId);

        task.AssigneeUserId.Should().Be(Beto.UserId);
    }

    [Fact]
    public async Task Create_ForARoomWithAnOpenTask_ShouldFail()
    {
        await CreateAsync();

        var act = () => CreateAsync();

        await act.Should().ThrowAsync<RoomAlreadyHasOpenCleaningTaskException>();
    }

    [Fact]
    public async Task Reassign_ShouldMoveTheTaskToTheNewAssignee()
    {
        var task = await CreateAsync(Ana.UserId);

        var reassigned = await _service.Handle(new AssignCleaningTaskCommand(task.Id, Beto.UserId, Reception));

        reassigned!.AssigneeUserId.Should().Be(Beto.UserId);
        (await _service.Handle(new AssignCleaningTaskCommand(task.Id, null, Reception)))!.AssigneeUserId
            .Should().Be(Ana.UserId, "an automatic reassignment picks someone other than the current assignee");
    }

    [Fact]
    public async Task StartAndComplete_ShouldTakeTheRoomToCleaningAndThenAvailable()
    {
        var task = await CreateAsync(Ana.UserId);

        await _service.Handle(new StartCleaningTaskCommand(task.Id, Ana));
        Room().Status.Should().Be(RoomStatus.Cleaning);

        var completed = await _service.Handle(new CompleteCleaningTaskCommand(task.Id, Ana));

        completed!.Status.Should().Be(CleaningTaskStatus.Completed);
        Room().Status.Should().Be(RoomStatus.Available);
        _context.Set<RoomStatusChange>().Where(c => c.RoomId == _roomId).Select(c => c.Origin)
            .Should().OnlyContain(origin => origin == RoomStatusChangeOrigin.Housekeeping);
        // Housekeeping's own changes never create another task.
        _context.Set<CleaningTask>().Should().ContainSingle();
    }

    [Fact]
    public async Task Start_AnUnassignedTaskByHousekeeping_ShouldTakeIt()
    {
        var task = await CreateAsync();

        var started = await _service.Handle(new StartCleaningTaskCommand(task.Id, Beto));

        started!.AssigneeUserId.Should().Be(Beto.UserId);
        started.Status.Should().Be(CleaningTaskStatus.InProgress);
    }

    [Fact]
    public async Task Start_WhenTheRoomIsOccupied_ShouldFail_AndLeaveTheTaskPending()
    {
        var task = await CreateAsync(Ana.UserId);
        await _rooms.Handle(new OccupyRoomForCheckInCommand(_roomId, 99, "guest@example.com"));

        var act = () => _service.Handle(new StartCleaningTaskCommand(task.Id, Ana));

        await act.Should().ThrowAsync<RoomOccupiedException>();
        _context.ChangeTracker.Clear();
        _context.Set<CleaningTask>().Single().Status.Should().Be(CleaningTaskStatus.Pending);
    }

    [Fact]
    public async Task ABlockingImpediment_ShouldReportAnIncident_AndKeepTheTaskOpenUntilItIsResolved()
    {
        var task = await CreateAsync(Ana.UserId);
        await _service.Handle(new StartCleaningTaskCommand(task.Id, Ana));

        var withImpediment = await _service.Handle(new ReportCleaningImpedimentCommand(task.Id, "Broken mirror", null,
            IncidentPriority.High, BlocksRoom: true, Ana));

        var incidentId = withImpediment!.ImpedimentIncidentIds.Should().ContainSingle().Subject;
        var incident = _context.Set<Incident>().Single(i => i.Id == incidentId);
        incident.RoomId.Should().Be(_roomId);
        incident.ReportedByUserId.Should().Be(Ana.UserId);
        Room().Status.Should().Be(RoomStatus.Maintenance);
        var complete = () => _service.Handle(new CompleteCleaningTaskCommand(task.Id, Ana));
        await complete.Should().ThrowAsync<RoomBlockedByIncidentException>();

        _context.ChangeTracker.Clear();
        _iam.Maintenance.Add(new UserContact(Maintenance.UserId, Maintenance.Email!, "Mario", UserRoles.Maintenance));
        await _incidents.Handle(new AssignIncidentCommand(incidentId, Maintenance.UserId, Maintenance));
        await _incidents.Handle(new StartIncidentCommand(incidentId, Maintenance));
        await _incidents.Handle(new ResolveIncidentCommand(incidentId, "Replaced.", Maintenance));

        // The room is back to Cleaning and keeps the same task (no duplicate), which can now be completed.
        Room().Status.Should().Be(RoomStatus.Cleaning);
        _context.Set<CleaningTask>().Should().ContainSingle();
        (await _service.Handle(new CompleteCleaningTaskCommand(task.Id, Ana)))!.Status.Should().Be(CleaningTaskStatus.Completed);
        Room().Status.Should().Be(RoomStatus.Available);
    }

    [Fact]
    public async Task ARoomThatGoesToCleaning_ShouldGetATask_AssignedAutomaticallyWhenTheHotelDoes()
    {
        await EnableAutoAssignAsync();

        await _rooms.Handle(new ChangeRoomStatusCommand(_roomId, RoomStatus.Cleaning, Reception.UserId, Reception.Email));

        var task = _context.Set<CleaningTask>().Should().ContainSingle().Subject;
        task.RoomId.Should().Be(_roomId);
        task.CreatedByUserId.Should().BeNull();
        task.AssigneeUserId.Should().Be(Ana.UserId);
        task.AssignedAutomatically.Should().BeTrue();
    }

    [Fact]
    public async Task ARoomMadeAvailableByStaff_ShouldCancelItsOpenTask()
    {
        await _rooms.Handle(new ChangeRoomStatusCommand(_roomId, RoomStatus.Cleaning, Reception.UserId, Reception.Email));

        await _rooms.Handle(new ChangeRoomStatusCommand(_roomId, RoomStatus.Available, Reception.UserId, Reception.Email));

        _context.Set<CleaningTask>().Single().Status.Should().Be(CleaningTaskStatus.Cancelled);
    }

    /// <summary>Routes the room events to the Housekeeping handler, as the real dispatcher does.</summary>
    private sealed class RoomEventsDispatcher(RoomCleaningTaskHandler handler) : IDomainEventDispatcher
    {
        public async Task DispatchAsync(IEnumerable<IEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            foreach (var e in domainEvents.OfType<RoomStatusChangedEvent>())
                await handler.HandleAsync(e, cancellationToken);
        }
    }

    private sealed class FakeIam : IIamContextFacade
    {
        public List<UserContact> Housekeeping { get; } = [];
        public List<UserContact> Maintenance { get; } = [];

        public Task<UserContact?> FetchUserContactAsync(int userId) =>
            Task.FromResult(Housekeeping.Concat(Maintenance).FirstOrDefault(contact => contact.UserId == userId));

        public Task<IReadOnlyList<UserContact>> ListHotelStaffAsync(int hotelId, IReadOnlyCollection<string> roles) =>
            Task.FromResult<IReadOnlyList<UserContact>>(Housekeeping.Concat(Maintenance)
                .Where(contact => roles.Contains(contact.Role)).ToList());

        public Task<int> FetchUserIdByEmail(string email) => throw new NotSupportedException();
        public Task<string> FetchEmailByUserId(int userId) => throw new NotSupportedException();

        public Task<ReissuedSession?> AssignHotelToAdministratorAsync(int userId, int hotelId, SessionContext currentSession) =>
            throw new NotSupportedException();
    }
}
