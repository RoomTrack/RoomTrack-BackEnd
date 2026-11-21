using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reqnroll;
using RoomTrack.Accommodations.API.Infrastructure.Persistence;
using RoomTrack.Accommodations.Application.Internal.CommandServices;
using RoomTrack.Accommodations.Application.Internal.Configuration;
using RoomTrack.Accommodations.Application.Internal.QueryServices;
using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Domain.Model.Entities;
using RoomTrack.Accommodations.Domain.Model.Exceptions;
using RoomTrack.Accommodations.Domain.Model.ValueObjects;
using RoomTrack.Accommodations.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Incidents.Application.Internal.CommandServices;
using RoomTrack.Incidents.Application.Internal.Configuration;
using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Commands;
using RoomTrack.Incidents.Domain.Model.Events;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Incidents.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Tests.Features.Support;

namespace RoomTrack.Tests.Features.Steps;

/// <summary>
///     Steps of US-08 and US-09 through the incident use cases, with the real Rooms context and an in-memory
///     database: a blocking incident and the status of its room change together, as in the deployed service.
/// </summary>
[Binding]
public sealed class IncidentSteps : IDisposable
{
    private static readonly IncidentActor Reception = new(10, "recepcion@roomtrack.pe", IsAdministrator: false);
    private static readonly IncidentActor Admin = new(1, "admin@roomtrack.pe", IsAdministrator: true);

    private static readonly Dictionary<string, IncidentActor> Staff = new()
    {
        ["Mario"] = new(20, "mario@roomtrack.pe", IsAdministrator: false),
        ["Lucía"] = new(30, "lucia@roomtrack.pe", IsAdministrator: false)
    };

    private static readonly Dictionary<string, IncidentPriority> Priorities = new()
    {
        ["baja"] = IncidentPriority.Low,
        ["media"] = IncidentPriority.Medium,
        ["alta"] = IncidentPriority.High,
        ["crítica"] = IncidentPriority.Critical
    };

    private static readonly Dictionary<string, IncidentStatus> Statuses = new()
    {
        ["pendiente"] = IncidentStatus.Pending,
        ["en curso"] = IncidentStatus.InProgress,
        ["resuelta"] = IncidentStatus.Resolved
    };

    private static readonly Dictionary<string, RoomStatus> RoomStatuses = new()
    {
        ["disponible"] = RoomStatus.Available,
        ["ocupada"] = RoomStatus.Occupied,
        ["limpieza"] = RoomStatus.Cleaning,
        ["mantenimiento"] = RoomStatus.Maintenance
    };

    private readonly ScenarioOutcome _outcome;
    private readonly AccommodationsDbContext _context;
    private readonly RecordingDispatcher _dispatcher = new();
    private readonly FakeIam _iam = new();
    private readonly RoomCommandService _rooms;
    private int _roomId;
    private Incident? _incident;

    public IncidentSteps(ScenarioOutcome outcome)
    {
        _outcome = outcome;
        _context = new AccommodationsDbContext(new DbContextOptionsBuilder<AccommodationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var roomRepository = new RoomRepository(_context);
        _rooms = new RoomCommandService(roomRepository, new RoomStatusChangeRepository(_context), new RoomTypeRepository(_context),
            null!, UnitOfWork(), Options.Create(new RoomOperationsSettings()), TimeProvider.System, null!);
    }

    public void Dispose() => _context.Dispose();

    // ── Given ───────────────────────────────────────────────────────

    [Given("la habitación {int} disponible en el hotel")]
    public void GivenAnAvailableRoom(int number)
    {
        var hotel = new Hotel();
        var roomType = new RoomType("Standard", "Standard room");
        _context.AddRange(hotel, roomType);
        _context.SaveChanges();
        var room = new Room(new CreateRoomCommand(hotel.Id, roomType.Id, 120m, $"Room {number}", [], number.ToString()));
        _context.Add(room);
        _context.SaveChanges();
        _roomId = room.Id;
    }

    [Given("{string} es personal de mantenimiento del hotel")]
    public void GivenIsMaintenanceStaff(string name) =>
        _iam.Maintenance.Add(new UserContact(Staff[name].UserId, Staff[name].Email!, name, UserRoles.Maintenance));

    [Given("que recepción reportó la incidencia {string} con prioridad {string}")]
    [When("recepción reporta la incidencia {string} con prioridad {string}")]
    public async Task WhenReceptionReports(string title, string priority) => await Report(title, priority, blocksRoom: false);

    [Given("que recepción reportó la incidencia {string} con prioridad {string} bloqueando la habitación")]
    [When("recepción reporta la incidencia {string} con prioridad {string} bloqueando la habitación")]
    public async Task WhenReceptionReportsABlockingIncident(string title, string priority) =>
        await Report(title, priority, blocksRoom: true);

    [Given("que {string} inició la incidencia")]
    [When("{string} inicia la incidencia")]
    public async Task WhenStartsTheIncident(string name) =>
        _incident = await Service().Handle(new StartIncidentCommand(_incident!.Id, Staff[name]));

    // ── When ────────────────────────────────────────────────────────

    [When("recepción intenta reportar la incidencia {string} con prioridad {string}")]
    public Task WhenReceptionTriesToReport(string title, string priority) =>
        _outcome.CaptureAsync(() => Report(title, priority, blocksRoom: false));

    [When("pasan {int} minutos sin que nadie inicie la incidencia")]
    public async Task WhenMinutesPassWithoutStarting(int minutes) =>
        await Service(new FixedTimeProvider(_incident!.ReportedAt.AddMinutes(minutes))).Handle(new EscalateLateIncidentsCommand());

    [When("{string} resuelve la incidencia con la nota {string}")]
    public async Task WhenResolves(string name, string notes) =>
        _incident = await Service().Handle(new ResolveIncidentCommand(_incident!.Id, notes, Staff[name]));

    [When("{string} intenta resolver la incidencia sin nota")]
    public Task WhenTriesToResolveWithoutNotes(string name) =>
        _outcome.CaptureAsync(() => Service().Handle(new ResolveIncidentCommand(_incident!.Id, null, Staff[name])));

    [When("el administrador asigna la incidencia a {string}")]
    public async Task WhenTheAdministratorAssigns(string name) =>
        _incident = await Service().Handle(new AssignIncidentCommand(_incident!.Id, Staff[name].UserId, Admin));

    [When("el administrador intenta asignar la incidencia a {string}")]
    public Task WhenTheAdministratorTriesToAssign(string name) =>
        _outcome.CaptureAsync(() => Service().Handle(new AssignIncidentCommand(_incident!.Id, Staff[name].UserId, Admin)));

    // ── Then ────────────────────────────────────────────────────────

    [Then("la incidencia queda {string}")]
    public void ThenTheIncidentIs(string status) => Stored().Status.Should().Be(Statuses[status]);

    [Then("la incidencia no tiene persona asignada")]
    public void ThenTheIncidentIsUnassigned() => Stored().AssigneeUserId.Should().BeNull();

    [Then("la incidencia queda asignada a {string}")]
    public void ThenTheIncidentIsAssignedTo(string name) => Stored().AssigneeUserId.Should().Be(Staff[name].UserId);

    [Then("la incidencia registra que la resolvió {string}")]
    public void ThenTheIncidentWasResolvedBy(string name)
    {
        var incident = Stored();
        incident.ResolvedByUserId.Should().Be(Staff[name].UserId);
        incident.ResolutionNotes.Should().NotBeNullOrWhiteSpace();
    }

    [Then("la incidencia se escala a los administradores")]
    public void ThenTheIncidentIsEscalated()
    {
        Stored().EscalatedAt.Should().NotBeNull();
        _dispatcher.Events.OfType<IncidentEscalatedEvent>().Should().ContainSingle();
    }

    [Then("la incidencia no se escala")]
    public void ThenTheIncidentIsNotEscalated()
    {
        Stored().EscalatedAt.Should().BeNull();
        _dispatcher.Events.OfType<IncidentEscalatedEvent>().Should().BeEmpty();
    }

    [Then("la habitación queda en estado {string}")]
    public void ThenTheRoomIs(string status) => Room().Status.Should().Be(RoomStatuses[status]);

    [Then("la habitación no puede volver a {string} mientras la incidencia siga abierta")]
    public async Task ThenTheRoomCannotGoBackWhileOpen(string status)
    {
        var makeAvailable = () => _rooms.Handle(new ChangeRoomStatusCommand(_roomId, RoomStatuses[status], Admin.UserId, Admin.Email));

        await makeAvailable.Should().ThrowAsync<RoomBlockedByIncidentException>();
        Room().IsBlockedByIncident.Should().BeTrue();
    }

    // ── Support ─────────────────────────────────────────────────────

    private async Task Report(string title, string priority, bool blocksRoom) =>
        _incident = await Service().Handle(new ReportIncidentCommand(_roomId, title, null, Priorities[priority], blocksRoom, Reception));

    private IncidentCommandService Service(TimeProvider? clock = null)
    {
        var roomRepository = new RoomRepository(_context);
        return new IncidentCommandService(new IncidentRepository(_context),
            new RoomQueryService(roomRepository, new RoomStatusChangeRepository(_context)), _rooms, _iam, UnitOfWork(),
            Options.Create(new IncidentSettings()), clock ?? TimeProvider.System);
    }

    private UnitOfWork UnitOfWork() => new(_context, _dispatcher);

    private Incident Stored() => _context.Set<Incident>().AsNoTracking().Single(i => i.Id == _incident!.Id);

    private Room Room() => _context.Set<Room>().AsNoTracking().Single(r => r.Id == _roomId);

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
            Task.FromResult<IReadOnlyList<UserContact>>(Maintenance);

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
