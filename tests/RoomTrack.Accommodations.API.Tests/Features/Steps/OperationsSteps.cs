using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Domain.Model.ValueObjects;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Contracts.Messaging;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Commands;
using RoomTrack.GuestRequests.Domain.Model.Queries;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.GuestRequests.Domain.Services;
using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Commands;
using RoomTrack.Housekeeping.Domain.Model.Exceptions;
using RoomTrack.Housekeeping.Domain.Model.Queries;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Housekeeping.Domain.Services;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Commands;
using RoomTrack.Incidents.Domain.Model.Queries;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Incidents.Domain.Services;
using RoomTrack.Messaging.Domain.Model.Commands;
using RoomTrack.Messaging.Domain.Model.Entities;
using RoomTrack.Messaging.Domain.Model.Queries;
using RoomTrack.Messaging.Domain.Model.ValueObjects;
using RoomTrack.Messaging.Domain.Services;
using RoomTrack.ShiftHandovers.Domain.Model.Aggregates;
using RoomTrack.ShiftHandovers.Domain.Model.Commands;
using RoomTrack.ShiftHandovers.Domain.Model.Queries;
using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;
using RoomTrack.ShiftHandovers.Domain.Services;
using RoomTrack.Tests.Features.Support;

namespace RoomTrack.Tests.Features.Steps;

/// <summary>
///     Steps of US-07 and of the Sprint 3 stories (US-10, US-11, US-12, US-19, US-20, US-23, US-30, US-32) through the
///     use cases of the Accommodations service wired as in production (<see cref="OperationsWorld"/>): each step is a
///     request with its own scope, and the push notifications are the ones the real event handlers send.
/// </summary>
[Binding]
public sealed class OperationsSteps(OperationsWorld world, ScenarioOutcome outcome)
{
    private static readonly Dictionary<string, string> Roles = new()
    {
        ["limpieza"] = UserRoles.Housekeeping,
        ["mantenimiento"] = UserRoles.Maintenance,
        ["recepción"] = UserRoles.Reception
    };

    private static readonly Dictionary<string, ServiceArea> Areas = new()
    {
        ["limpieza"] = ServiceArea.Housekeeping,
        ["mantenimiento"] = ServiceArea.Maintenance,
        ["recepción"] = ServiceArea.Reception
    };

    private readonly Dictionary<string, (int Id, string Role)> _people = new();
    private int _nextUserId = 100;
    private CleaningTask? _task;
    private Incident? _incident;
    private GuestRequest? _request;
    private ConversationMessage? _message;
    private ShiftHandover? _handover;
    private RoomMaintenanceHistory? _history;
    private IReadOnlyList<GuestRequest> _board = [];
    private IReadOnlyList<IncidentAlert> _alerts = [];
    private IReadOnlyList<ConversationMessage> _thread = [];
    private Func<Task>? _pending;
    private string _requestTitle = "Toallas extra";
    private ServiceArea _requestArea = ServiceArea.Housekeeping;

    // ── People and places ───────────────────────────────────────────

    [Given("un hotel con la suite {int} y la habitación estándar {int}")]
    public void GivenAHotelWithASuiteAndAStandardRoom(int suite, int standard)
    {
        world.AHotelWithRooms("Suite", suite);
        world.AHotelWithRooms("Estándar", standard);
    }

    [Given("un hotel con la habitación {int}")]
    public void GivenAHotelWithARoom(int number) => world.AHotelWithRooms("Estándar", number);

    [Given("un hotel con la habitación {int} y la habitación {int}")]
    public void GivenAHotelWithTwoRooms(int first, int second) => world.AHotelWithRooms("Estándar", first, second);

    [Given("{string} trabaja en {word}")]
    public void GivenWorksIn(string name, string area) => Person(name, Roles[area]);

    [Given("{string} es administradora del hotel")]
    public void GivenIsAdministrator(string name) => Person(name, UserRoles.Admin);

    [Given("el huésped {string} se aloja en la habitación {int}")]
    public void GivenAGuestStays(string name, int room)
    {
        var guest = Person(name, UserRoles.Guest, registerAsStaff: false);
        world.Reservations.Current.Add(new GuestStay(900, "RT-TEST01", world.HotelId, world.Rooms[room], guest.Id,
            OperationsWorld.Start.Date, OperationsWorld.Start.Date.AddDays(3), CheckedIn: true));
    }

    // ── US-07: cleaning checklist ───────────────────────────────────

    [Given("el hotel configuró la checklist de las suites:")]
    public Task GivenTheSuiteChecklist(DataTable steps) =>
        world.Request<ICleaningTaskCommandService, CleaningChecklistTemplate>(service => service.Handle(
            new ConfigureCleaningChecklistCommand(world.HotelId, world.RoomTypes["Suite"],
                steps.Rows.Select(row => new ChecklistItemDefinition(row["paso"], row["obligatorio"] == "sí")).ToList(), 1)));

    [Given("que recepción creó la tarea de limpieza de la habitación {int} para {string}")]
    public async Task GivenReceptionCreatedTheTask(int room, string housekeeper) =>
        _task = await CreateCleaningTask(room, Id(housekeeper));

    [When("{string} abre la tarea")]
    public async Task WhenOpensTheTask(string name) =>
        _task = await world.Request<ICleaningTaskQueryService, CleaningTask?>(service =>
            service.Handle(new GetCleaningTaskByIdQuery(_task!.Id)));

    [Then("ve la checklist específica para suites")]
    public void ThenSeesTheSuiteChecklist()
    {
        _task!.Checklist.Select(item => (item.Label, item.IsRequired)).Should().Equal(
            ("Cambiar ropa de cama", true), ("Desinfectar el jacuzzi", true), ("Reponer frutas", false));
        _task.ChecklistProgress.Should().Be(0);
    }

    [Then("una tarea de la habitación {int} no tiene la checklist de las suites")]
    public async Task ThenAStandardRoomTaskHasNoSuiteChecklist(int room) =>
        (await CreateCleaningTask(room, assignee: null)).Checklist.Should().BeEmpty("the hotel configured no protocol for that type");

    [Given("que {string} inició la limpieza de la habitación {int}")]
    public async Task GivenStartedTheCleaning(string name, int room)
    {
        _task = await CreateCleaningTask(room, Id(name));
        _task = await world.Request<ICleaningTaskCommandService, CleaningTask?>(service =>
            service.Handle(new StartCleaningTaskCommand(_task.Id, Housekeeper(name))));
    }

    [Given("{string} marcó el paso {string}")]
    [When("{string} marca el paso {string}")]
    public async Task WhenChecksTheStep(string name, string step)
    {
        var itemId = _task!.Checklist.Single(item => item.Label == step).Id;
        _task = await world.Request<ICleaningTaskCommandService, CleaningTask?>(service =>
            service.Handle(new MarkCleaningChecklistItemCommand(_task.Id, itemId, Done: true, Housekeeper(name))));
    }

    [Then("el progreso de la checklist es {int} %")]
    public void ThenTheProgressIs(int percent) => _task!.ChecklistProgress.Should().Be(percent);

    [When("{string} intenta marcar la habitación como lista")]
    public Task WhenTriesToFinish(string name) =>
        outcome.CaptureAsync(() => world.Request<ICleaningTaskCommandService, CleaningTask?>(service =>
            service.Handle(new CompleteCleaningTaskCommand(_task!.Id, Housekeeper(name)))));

    [Then("se señala lo pendiente: {string}")]
    public void ThenThePendingStepIsPointedOut(string step)
    {
        outcome.Error.Should().BeOfType<CleaningChecklistIncompleteException>()
            .Which.PendingItems.Should().Equal(step);
        world.Read(context => context.Set<CleaningTask>().Single(task => task.Id == _task!.Id).Status)
            .Should().Be(CleaningTaskStatus.InProgress);
    }

    // ── US-10, US-30, US-23: incidents, preventive maintenance and alerts ──

    [Given("que en la habitación {int} se reportaron {string} y {string}")]
    public async Task GivenTwoIncidents(int room, string first, string second)
    {
        await ReportIncident(room, first, IncidentPriority.High);
        world.Clock.Now = world.Clock.Now.AddHours(2);
        await ReportIncident(room, second, IncidentPriority.Low);
    }

    [Given("{string} resolvió {string}")]
    public async Task GivenResolved(string name, string title)
    {
        var incidentId = world.Read(context => context.Set<Incident>().Single(incident => incident.Title == title).Id);
        await ResolveIncident(incidentId, name, "Empaque cambiado");
    }

    [When("abro el historial de la habitación {int}")]
    public async Task WhenIOpenTheRoomHistory(int room) =>
        _history = await world.Request<IPreventiveMaintenanceQueryService, RoomMaintenanceHistory>(service =>
            service.Handle(new GetRoomMaintenanceHistoryQuery(world.Rooms[room], world.HotelId)));

    [Then("veo todas las incidencias reportadas con fechas y estados")]
    public void ThenISeeEveryIncident()
    {
        _history!.Incidents.Select(incident => (incident.Title, incident.Status)).Should().BeEquivalentTo(
            [("Foco quemado", IncidentStatus.Pending), ("Ducha con fuga", IncidentStatus.Resolved)]);
        _history.Incidents.Select(incident => incident.ReportedAt).Should().BeInDescendingOrder();
        _history.Incidents.Single(incident => incident.Title == "Ducha con fuga").ResolvedAt.Should().NotBeNull();
    }

    [Given("que la habitación {int} acumula {int} incidencias {string} este mes")]
    public async Task GivenRepeatedIncidents(int room, int count, string title)
    {
        for (var i = 0; i < count; i++)
        {
            await ReportIncident(room, title, IncidentPriority.High);
            world.Clock.Now = world.Clock.Now.AddDays(5);
        }
    }

    [When("se reporta una nueva incidencia {string} en la habitación {int}")]
    public Task WhenANewIncidentIsReported(string title, int room) => ReportIncident(room, title, IncidentPriority.High);

    [Then("el sistema resalta el patrón como alerta")]
    public void ThenThePatternIsHighlighted()
    {
        _incident!.IsRecurring.Should().BeTrue();
        _incident.RecurrenceCount.Should().Be(4);
        _incident.History.Should().Contain(entry => entry.Action == IncidentHistoryAction.RecurrenceDetected);
    }

    [Then("{string} recibe la alerta de incidencia recurrente")]
    public void ThenReceivesTheRecurrenceAlert(string name) =>
        world.Push.To(Id(name)).Should().Contain(message => message.Title == "Incidencia recurrente" && message.Urgent);

    [Given("que {string} define la tarea preventiva {string} de la habitación {int} cada {int} días desde el {word}")]
    public Task GivenAPreventivePlan(string name, string title, int room, int days, string firstDue) =>
        world.Request<IPreventiveMaintenanceCommandService, PreventiveMaintenancePlan>(service => service.Handle(
            new SchedulePreventiveMaintenanceCommand(world.Rooms[room], title, null, days, Date(firstDue), 3, Maintenance(name))));

    [Given("llegó el {word}")]
    [When("llega el {word}")]
    public async Task WhenTheDayArrives(string day)
    {
        var date = Date(day);
        world.Clock.Now = new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero);
        await world.Request<IPreventiveMaintenanceCommandService, PreventiveMaintenanceRunResult>(service =>
            service.Handle(new RunPreventiveMaintenanceCommand()));
    }

    [Then("el sistema genera la tarea preventiva para mantenimiento")]
    public void ThenThePreventiveTaskIsGenerated()
    {
        var task = PreventiveIncidents().Should().ContainSingle().Subject;
        task.Status.Should().Be(IncidentStatus.Pending);
        task.PreventivePlanId.Should().NotBeNull();
        world.Push.Sent.Should().Contain(message => message.Title == "Nueva incidencia", "maintenance is notified of the new task");
    }

    [Then("la siguiente se programa para el {word}")]
    public void ThenTheNextIsScheduledFor(string day) =>
        world.Read(context => context.Set<PreventiveMaintenancePlan>().Single()).NextDueOn.Should().Be(Date(day));

    [Then("{string} recibe un recordatorio de {string}")]
    public void ThenReceivesAReminder(string name, string title) =>
        world.Push.To(Id(name)).Should().ContainSingle(message => message.Title == "Mantenimiento preventivo próximo")
            .Which.Body.Should().Contain(title);

    [Then("todavía no se genera la tarea")]
    public void ThenTheTaskIsNotGeneratedYet() => PreventiveIncidents().Should().BeEmpty();

    [Then("veo tanto incidencias correctivas como mantenimientos preventivos realizados")]
    public void ThenISeeCorrectiveAndPreventive()
    {
        _history!.Incidents.Count(incident => incident.Kind == IncidentKind.Corrective).Should().Be(2);
        _history.Incidents.Should().ContainSingle(incident => incident.Kind == IncidentKind.Preventive)
            .Which.Title.Should().Be("Limpiar filtros del aire");
        _history.Plans.Should().ContainSingle().Which.GeneratedCount.Should().Be(1);
    }

    /// <summary>The guest of the room arrives at the hotel's check-in time today (3 hours from the start).</summary>
    private static readonly DateTimeOffset ArrivalAt = OperationsWorld.Start.AddHours(3);

    [Given("que una habitación con checkout inminente no está limpia")]
    public async Task GivenARoomNotCleanForAnArrival()
    {
        // The previous guest left: the room is waiting for its cleaning, and the next guest arrives today.
        var roomId = world.Rooms[204];
        await world.Request<IRoomCommandService, Room?>(service =>
            service.Handle(new ChangeRoomStatusCommand(roomId, RoomStatus.Cleaning, Reception.Id, Email("Recepción"))));
        world.Reservations.Arrivals.Add(new ExpectedArrival(910, "RT-ARRIVE", world.HotelId, roomId, ArrivalAt));
    }

    [When("se acerca la hora")]
    public async Task WhenTheArrivalIsNear()
    {
        world.Clock.Now = ArrivalAt.AddMinutes(-90);
        // The scheduler runs every hour: the next run must not alert the same arrival again.
        _arrivalAlerts = await world.Request<IRoomCommandService, int>(service => service.Handle(new RaiseArrivalReadinessAlertsCommand()));
        _arrivalAlerts += await world.Request<IRoomCommandService, int>(service => service.Handle(new RaiseArrivalReadinessAlertsCommand()));
    }

    [Then("recibo una alerta")]
    public void ThenIGetAnAlert()
    {
        _arrivalAlerts.Should().Be(1, "one alert per arrival");
        world.Push.To(Id("Elena")).Should().ContainSingle(message => message.Title == "Habitación no lista para una llegada")
            .Which.Body.Should().Contain("204").And.Contain("RT-ARRIVE");
        world.Emails.Sent.Should().ContainSingle(email => email.To == Email("Elena") && email.Subject.Contains("no está lista"));
        world.Read(context => context.Set<Room>().Single(room => room.Id == world.Rooms[204])).ArrivalAlertBookingId.Should().Be(910);
    }

    private int _arrivalAlerts;

    [Given("que una incidencia urgente lleva más tiempo del definido sin resolverse")]
    public Task GivenACriticalIncidentWaits() => ReportIncident(world.Rooms.Keys.First(), "Cerradura rota", IncidentPriority.Critical);

    [When("se cumple el plazo")]
    public async Task WhenTheDeadlinePasses()
    {
        world.Clock.Now = world.Clock.Now.AddMinutes(61);
        await world.Request<IIncidentCommandService, int>(service => service.Handle(new EscalateLateIncidentsCommand()));
    }

    [Then("se genera una alerta automática")]
    public void ThenAnAlertIsRaised()
    {
        world.Read(context => context.Set<Incident>().Single(incident => incident.Id == _incident!.Id)).EscalatedAt.Should().NotBeNull();
        world.Push.To(Id("Elena")).Should().Contain(message => message.Category == PushCategories.IncidentEscalated);
    }

    [Given("que quiero revisar alertas pasadas")]
    public async Task GivenPastAlerts()
    {
        await GivenACriticalIncidentWaits();
        await WhenTheDeadlinePasses();
        await ResolveIncident(_incident!.Id, "Mario", "Cerradura reemplazada");
        await GivenRepeatedIncidents(world.Rooms.Keys.First(), 4, "Aire acondicionado no enfría");
    }

    [When("accedo al registro")]
    public async Task WhenIOpenTheAlertLog() =>
        _alerts = await world.Request<IIncidentQueryService, IReadOnlyList<IncidentAlert>>(service => service.Handle(
            new GetIncidentAlertsQuery(world.HotelId, OperationsWorld.Start.AddDays(-1), world.Clock.Now.AddDays(1))));

    [Then("veo todas las alertas generadas con su resolución")]
    public void ThenISeeEveryAlertWithItsResolution()
    {
        _alerts.Select(alert => alert.Kind).Should().BeEquivalentTo(["Escalation", "Recurrence"]);
        var escalation = _alerts.Single(alert => alert.Kind == "Escalation").Incident;
        escalation.Status.Should().Be(IncidentStatus.Resolved);
        escalation.ResolutionNotes.Should().Be("Cerradura reemplazada");
        _alerts.Select(alert => alert.RaisedAt).Should().BeInDescendingOrder();
    }

    // ── US-11, US-19: guest requests ────────────────────────────────

    [Given("que un huésped pide {string}")]
    public void GivenAGuestAsksFor(string title) => _requestTitle = title;

    [When("registro la solicitud para el área de {string}")]
    public async Task WhenIRegisterTheRequest(string area)
    {
        _requestArea = Areas[area];
        await RegisterRequest();
    }

    [Then("se crea una tarea con el área destino asignada")]
    public void ThenATaskForTheAreaIsCreated()
    {
        var request = StoredRequest();
        request.Area.Should().Be(ServiceArea.Housekeeping);
        request.Status.Should().Be(GuestRequestStatus.Pending);
        request.Title.Should().Be("Almohada extra");
        request.GuestUserId.Should().Be(Id("Luis"), "the guest staying in the room follows it in the app");
    }

    [Given("que clasifico la solicitud {string} como {string}")]
    public void GivenIClassifyTheRequest(string title, string area)
    {
        _requestTitle = title;
        _requestArea = Areas[area];
    }

    [When("la guardo")]
    public Task WhenISaveIt() => RegisterRequest();

    [Then("se notifica automáticamente al personal de mantenimiento")]
    public void ThenMaintenanceIsNotified()
    {
        world.Push.To(Id("Mario")).Should().ContainSingle(message => message.Category == PushCategories.GuestRequest)
            .Which.Body.Should().Be("El grifo gotea");
        world.Push.To(Id("Ana")).Should().BeEmpty("housekeeping is not the area of the request");
    }

    [Given("que una solicitud está en curso")]
    public async Task GivenARequestInProgress()
    {
        await RegisterRequest();
        await StartRequest("Ana");
    }

    [When("reviso el tablero")]
    public async Task WhenIReviewTheBoard() =>
        _board = await world.Request<IGuestRequestQueryService, IReadOnlyList<GuestRequest>>(service =>
            service.Handle(new GetGuestRequestsQuery(world.HotelId)));

    [Then("veo su estado actualizado")]
    public void ThenISeeItsCurrentStatus() =>
        _board.Should().ContainSingle(request => request.Id == _request!.Id).Which.Status.Should().Be(GuestRequestStatus.InProgress);

    [Given("que la solicitud se resuelve")]
    public Task GivenTheRequestIsBeingResolved() => GivenARequestInProgress();

    [When("actualizo el estado")]
    public async Task WhenIUpdateTheStatus() =>
        _request = await world.Request<IGuestRequestCommandService, GuestRequest?>(service =>
            service.Handle(new ResolveGuestRequestCommand(_request!.Id, "Se entregó en la habitación", RequestActor("Ana"))));

    [Then("el huésped recibe una confirmación")]
    public void ThenTheGuestGetsAConfirmation() =>
        world.Push.To(Id("Luis")).Should().Contain(message => message.Title == "Tu solicitud fue atendida");

    [Given("que necesito algo durante mi estancia")]
    public void GivenINeedSomething() => _requestTitle = "Toallas extra";

    [When("completo el formulario de solicitud")]
    public Task WhenIFillTheForm() => SendRequestAsGuest();

    [Then("esta llega automáticamente a recepción")]
    public void ThenItReachesTheFrontDesk()
    {
        StoredRequest().Area.Should().Be(ServiceArea.Reception);
        StoredRequest().CreatedByGuest.Should().BeTrue();
        world.Push.To(Id("Rocío")).Should().Contain(message => message.Category == PushCategories.GuestRequest);
    }

    [Given("que envié una solicitud")]
    public void GivenISentARequest() => _pending = SendRequestAsGuest;

    [When("se registra")]
    public Task WhenItIsRegistered() => _pending!();

    [Then("recibo una confirmación con un tiempo estimado de atención")]
    public void ThenIGetAConfirmationWithTheEstimatedTime()
    {
        StoredRequest().EstimatedAttentionBy.Should().BeAfter(world.Clock.Now);
        world.Push.To(Id("Luis")).Should().ContainSingle(message => message.Title == "Recibimos tu solicitud")
            .Which.Body.Should().MatchRegex(@"unos \d+ minutos");
    }

    [Given("que mi solicitud está en proceso")]
    public async Task GivenMyRequestIsInProgress()
    {
        await SendRequestAsGuest();
        _request = await world.Request<IGuestRequestCommandService, GuestRequest?>(service =>
            service.Handle(new RouteGuestRequestCommand(_request!.Id, ServiceArea.Housekeeping, RequestActor("Rocío"))));
        await StartRequest("Ana");
    }

    [When("consulto su estado")]
    public async Task WhenICheckItsStatus() =>
        _board = await world.Request<IGuestRequestQueryService, IReadOnlyList<GuestRequest>>(service =>
            service.Handle(new GetGuestRequestsOfGuestQuery(Id("Luis"))));

    [Then("veo si está pendiente, en proceso o resuelta")]
    public void ThenISeeTheStatus()
    {
        var mine = _board.Should().ContainSingle().Subject;
        mine.Status.Should().Be(GuestRequestStatus.InProgress);
        world.Push.To(Id("Luis")).Should().Contain(message => message.Title == "Estamos atendiendo tu solicitud");
    }

    // ── US-20, US-12: conversations ─────────────────────────────────

    [Given("que tengo una consulta")]
    public void GivenIHaveAQuestion() { }

    [When("envío un mensaje desde el chat")]
    public Task WhenISendAChatMessage() => WriteAsGuest();

    [Then("la recepción lo recibe y puede responderme en la misma conversación")]
    public async Task ThenTheFrontDeskReceivesItAndAnswers()
    {
        world.Push.To(Id("Rocío")).Should().Contain(message => message.Title == "Mensaje de huésped: Luis");
        var answer = await world.Request<IMessagingCommandService, ConversationMessage?>(service =>
            service.Handle(new PostMessageCommand(_message!.ConversationId, "Claro, a las 13:00.", ConversationActorOf("Rocío"))));
        answer!.ConversationId.Should().Be(_message!.ConversationId);
        (await Messages(_message.ConversationId)).OrderBy(message => message.Id).Select(message => message.SenderName)
            .Should().Equal("Luis", "Rocío");
    }

    [Given("que recepción responde mi mensaje")]
    public async Task GivenTheFrontDeskAnswers()
    {
        await WriteAsGuest();
        _pending = () => world.Request<IMessagingCommandService, ConversationMessage?>(service =>
            service.Handle(new PostMessageCommand(_message!.ConversationId, "Claro, a las 13:00.", ConversationActorOf("Rocío"))));
    }

    [When("esto ocurre")]
    public Task WhenItHappens() => _pending!();

    [Then("recibo una notificación en mi dispositivo")]
    public void ThenIGetANotification() =>
        world.Push.To(Id("Luis")).Should().ContainSingle(message => message.Title == "Respuesta de recepción")
            .Which.Category.Should().Be(PushCategories.Message);

    [Given("que tengo una tarea asignada")]
    public async Task GivenIHaveAnAssignedTask() => _task = await CreateCleaningTask(world.Rooms.Keys.First(), Id("Ana"));

    [When("necesito aclarar un detalle")]
    public Task WhenINeedToClarify() => Comment("Rocío", "¿La cama extra va junto a la ventana?");

    [Then("puedo comentar directamente sobre esa tarea y notificar al responsable")]
    public void ThenICommentAndTheAssigneeIsNotified() =>
        world.Push.To(Id("Ana")).Should().Contain(message => message.Title == "Comentario de Rocío"
                                                             && message.Body.Contains("cama extra"));

    [Given("que necesito comunicarme con todo el equipo de limpieza")]
    public void GivenINeedTheWholeTeam() { }

    [When("envío un mensaje al canal del área")]
    public async Task WhenIWriteToTheAreaChannel() =>
        _message = await world.Request<IMessagingCommandService, ConversationMessage>(service => service.Handle(
            new WriteToAreaCommand(world.HotelId, ServiceArea.Housekeeping, "Hoy llega un grupo de 20 personas: refuercen el 3er piso.",
                ConversationActorOf("Ana"))));

    [Then("todos los miembros lo reciben")]
    public void ThenEveryMemberReceivesIt() =>
        world.Push.Sent.Should().ContainSingle(message => message.Category == PushCategories.Message)
            .Which.UserIds.Should().BeEquivalentTo([Id("Rosa"), Id("Carla")], "every other member of the area");

    [Given("que reviso una tarea cerrada")]
    public async Task GivenAClosedTask()
    {
        await GivenIHaveAnAssignedTask();
        await Comment("Rocío", "El huésped pidió cama extra.");
        _task = await world.Request<ICleaningTaskCommandService, CleaningTask?>(service =>
            service.Handle(new StartCleaningTaskCommand(_task!.Id, Housekeeper("Ana"))));
        await Comment("Ana", "Cama extra lista.");
        _task = await world.Request<ICleaningTaskCommandService, CleaningTask?>(service =>
            service.Handle(new CompleteCleaningTaskCommand(_task!.Id, Housekeeper("Ana"))));
    }

    [When("abro el historial de la tarea")]
    public async Task WhenIOpenTheTaskHistory()
    {
        var thread = await world.Request<IMessagingQueryService, RoomTrack.Messaging.Domain.Model.Aggregates.Conversation?>(service =>
            service.Handle(new GetTaskThreadQuery(ThreadSubject.CleaningTask, _task!.Id)));
        _thread = await Messages(thread!.Id);
        _task = await world.Request<ICleaningTaskQueryService, CleaningTask?>(service =>
            service.Handle(new GetCleaningTaskByIdQuery(_task!.Id)));
    }

    [Then("veo todos los comentarios y cambios de estado relacionados")]
    public void ThenISeeCommentsAndStatusChanges()
    {
        _thread.OrderBy(message => message.Id).Select(message => message.Body)
            .Should().Equal("El huésped pidió cama extra.", "Cama extra lista.");
        _task!.Status.Should().Be(CleaningTaskStatus.Completed);
        _task.History.Select(entry => entry.Action).Should().ContainInOrder(CleaningTaskHistoryAction.Created,
            CleaningTaskHistoryAction.Assigned, CleaningTaskHistoryAction.Started, CleaningTaskHistoryAction.Completed);
    }

    // ── US-32: shift handover ───────────────────────────────────────

    [Given("{string} tiene abierta la limpieza de la habitación {int}")]
    public async Task GivenHasAnOpenCleaning(string name, int room) => await CreateCleaningTask(room, Id(name));

    [Given("{string} reportó la incidencia {string} en la habitación {int}")]
    public Task GivenReportedTheIncident(string name, string title, int room) =>
        world.Request<IIncidentCommandService, Incident>(service => service.Handle(
            new ReportIncidentCommand(world.Rooms[room], title, null, IncidentPriority.Medium, false,
                new RoomTrack.Incidents.Domain.Model.ValueObjects.IncidentActor(Id(name), Email(name), false))));

    [Given("que termino mi turno")]
    public void GivenMyShiftEnds() { }

    [When("completo el reporte de entrega")]
    public Task WhenICompleteTheHandover() => HandOver("Ana");

    [Then("quedan registradas las tareas e incidencias abiertas")]
    public void ThenTheOpenItemsAreRecorded()
    {
        var stored = world.Read(context => context.Set<ShiftHandover>().Include(h => h.Items).Single());
        stored.Area.Should().Be(ShiftArea.Housekeeping);
        stored.Items.Select(item => (item.Kind, item.RoomId)).Should().BeEquivalentTo([
            (ShiftHandoverItemKind.CleaningTask, world.Rooms[101]),
            (ShiftHandoverItemKind.Incident, world.Rooms[102])]);
        stored.Items.Single(item => item.Kind == ShiftHandoverItemKind.Incident).Summary.Should().Be("Persiana trabada");
    }

    [Given("que inicio un nuevo turno")]
    public Task GivenANewShiftStarts() => HandOver("Ana");

    [When("accedo a mi panel")]
    public async Task WhenIOpenMyPanel() =>
        _handover = await world.Request<IShiftHandoverQueryService, ShiftHandover?>(service =>
            service.Handle(new GetLatestShiftHandoverQuery(world.HotelId, ShiftArea.Housekeeping)));

    [Then("veo el resumen dejado por el turno anterior")]
    public void ThenISeeThePreviousSummary()
    {
        _handover!.AuthorUserId.Should().Be(Id("Ana"));
        _handover.Notes.Should().Be("La 101 queda a medias: falta el baño.");
        _handover.Items.Should().HaveCount(2);
        world.Push.To(Id("Rosa")).Should().Contain(message => message.Title == "Entrega de turno");
    }

    [Given("que revisé la entrega de turno")]
    public async Task GivenIReadTheHandover()
    {
        await HandOver("Ana");
        await WhenIOpenMyPanel();
    }

    [When("la marco como leída")]
    public async Task WhenIMarkItAsRead() =>
        _handover = await world.Request<IShiftHandoverCommandService, ShiftHandover?>(service =>
            service.Handle(new AcknowledgeShiftHandoverCommand(_handover!.Id, ShiftActor("Rosa"))));

    [Then("queda registrado quién y cuándo lo confirmó")]
    public void ThenWhoAndWhenAreRecorded()
    {
        var stored = world.Read(context => context.Set<ShiftHandover>().Include(h => h.Acknowledgements).Single());
        var acknowledgement = stored.Acknowledgements.Should().ContainSingle().Subject;
        acknowledgement.UserId.Should().Be(Id("Rosa"));
        acknowledgement.AcknowledgedAt.Should().Be(world.Clock.Now);
    }

    // ── Support ─────────────────────────────────────────────────────

    private (int Id, string Role) Person(string name, string role, bool registerAsStaff = true)
    {
        var person = (_nextUserId++, role);
        _people[name] = person;
        if (registerAsStaff) world.Iam.Add(person.Item1, name, role);
        return person;
    }

    private int Id(string name) => _people[name].Id;

    private static string Email(string name) => $"{name.ToLowerInvariant()}@roomtrack.pe";

    private (int Id, string Role) Reception =>
        _people.TryGetValue("Recepción", out var reception) ? reception : Person("Recepción", UserRoles.Reception);

    private CleaningTaskActor Housekeeper(string name) => new(Id(name), Email(name), CleaningTaskRole.Housekeeper);

    private IncidentActor Maintenance(string name) => new(Id(name), Email(name), IsAdministrator: false);

    private GuestRequestActor RequestActor(string name) => new(Id(name), Email(name), _people[name].Role);

    private ConversationActor ConversationActorOf(string name) =>
        new(Id(name), name, _people[name].Role, _people[name].Role == UserRoles.Guest ? null : world.HotelId);

    private ShiftHandoverActor ShiftActor(string name) => new(Id(name), Email(name), ShiftArea.Housekeeping);

    private static DateOnly Date(string day) => DateOnly.ParseExact(day, "dd/MM/yyyy", CultureInfo.InvariantCulture);

    private Task<CleaningTask> CreateCleaningTask(int room, int? assignee) =>
        world.Request<ICleaningTaskCommandService, CleaningTask>(service => service.Handle(new CreateCleaningTaskCommand(
            world.Rooms[room], CleaningTaskPriority.Normal, null, assignee,
            new CleaningTaskActor(Reception.Id, "recepcion@roomtrack.pe", CleaningTaskRole.FrontDesk))));

    private async Task ReportIncident(int room, string title, IncidentPriority priority) =>
        _incident = await world.Request<IIncidentCommandService, Incident>(service => service.Handle(
            new ReportIncidentCommand(world.Rooms[room], title, null, priority, false,
                new IncidentActor(Reception.Id, "recepcion@roomtrack.pe", IsAdministrator: false))));

    private async Task ResolveIncident(int incidentId, string name, string notes)
    {
        await world.Request<IIncidentCommandService, Incident?>(service =>
            service.Handle(new StartIncidentCommand(incidentId, Maintenance(name))));
        await world.Request<IIncidentCommandService, Incident?>(service =>
            service.Handle(new ResolveIncidentCommand(incidentId, notes, Maintenance(name))));
    }

    private IReadOnlyList<Incident> PreventiveIncidents() =>
        world.Read(context => context.Set<Incident>().Where(incident => incident.Kind == IncidentKind.Preventive).ToList());

    private async Task RegisterRequest() =>
        _request = await world.Request<IGuestRequestCommandService, GuestRequest>(service => service.Handle(
            new RegisterGuestRequestCommand(world.Rooms.Values.First(), _requestTitle, null, _requestArea,
                new GuestRequestActor(Reception.Id, "recepcion@roomtrack.pe", UserRoles.Reception))));

    private async Task SendRequestAsGuest() =>
        _request = await world.Request<IGuestRequestCommandService, GuestRequest>(service =>
            service.Handle(new SendGuestRequestCommand(_requestTitle, null, null, RequestActor("Luis"))));

    private async Task StartRequest(string name) =>
        _request = await world.Request<IGuestRequestCommandService, GuestRequest?>(service =>
            service.Handle(new StartGuestRequestCommand(_request!.Id, RequestActor(name))));

    private GuestRequest StoredRequest() =>
        world.Read(context => context.Set<GuestRequest>().Single(request => request.Id == _request!.Id));

    private async Task WriteAsGuest() =>
        _message = await world.Request<IMessagingCommandService, ConversationMessage>(service =>
            service.Handle(new WriteToFrontDeskCommand("¿Puedo dejar mi equipaje después del check-out?", ConversationActorOf("Luis"))));

    private Task<IReadOnlyList<ConversationMessage>> Messages(int conversationId) =>
        world.Request<IMessagingQueryService, IReadOnlyList<ConversationMessage>>(service =>
            service.Handle(new GetMessagesQuery(conversationId)));

    private Task Comment(string name, string body) =>
        world.Request<IMessagingCommandService, ConversationMessage?>(service =>
            service.Handle(new CommentOnTaskCommand(ThreadSubject.CleaningTask, _task!.Id, body, ConversationActorOf(name))));

    private async Task HandOver(string name)
    {
        world.Clock.Now = world.Clock.Now.AddHours(8);
        _handover = await world.Request<IShiftHandoverCommandService, ShiftHandover>(service => service.Handle(
            new CreateShiftHandoverCommand(world.HotelId, null, "La 101 queda a medias: falta el baño.", ShiftActor(name))));
    }
}
