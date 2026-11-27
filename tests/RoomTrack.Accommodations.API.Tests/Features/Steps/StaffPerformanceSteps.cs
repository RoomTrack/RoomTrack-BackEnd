using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reqnroll;
using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Commands;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Housekeeping.Domain.Services;
using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Commands;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Incidents.Domain.Services;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.StaffPerformance.Domain.Model.Aggregates;
using RoomTrack.StaffPerformance.Domain.Model.Commands;
using RoomTrack.StaffPerformance.Interfaces.REST;
using RoomTrack.StaffPerformance.Domain.Model.Queries;
using RoomTrack.StaffPerformance.Domain.Model.ValueObjects;
using RoomTrack.StaffPerformance.Domain.Services;
using RoomTrack.Tests.Features.Support;

namespace RoomTrack.Tests.Features.Steps;

/// <summary>
///     Steps of US-24 (staff productivity) and US-25 (service times). The work is done through the real use cases of
///     Housekeeping and Incidents, moving the clock between each change, and the reports are read through the
///     StaffPerformance query service. The people and the hotel come from the steps of <see cref="OperationsSteps"/>.
/// </summary>
[Binding]
public sealed class StaffPerformanceSteps(OperationsWorld world)
{
    private const int ReceptionUserId = 1;
    private const int PeriodDays = 30;

    private static readonly Dictionary<string, WorkArea> Areas = new()
    {
        ["limpieza"] = WorkArea.Housekeeping,
        ["mantenimiento"] = WorkArea.Maintenance,
        ["recepción"] = WorkArea.FrontDesk
    };

    private int _nextRoom;
    private ProductivityReport? _productivity;
    private ServiceTimesReport? _serviceTimes;
    private string _csv = string.Empty;
    private ReportSchedule? _schedule;
    private DateTimeOffset _scheduledRun;
    private int _sent;

    // ── Work done ───────────────────────────────────────────────────

    [Given("que {string} completó {int} limpieza(s)")]
    public async Task GivenCompletedCleanings(string name, int count)
    {
        for (var i = 0; i < count; i++) await Clean(name, wait: TimeSpan.FromMinutes(10), work: TimeSpan.FromMinutes(30));
    }

    [Given("que {string} completó {int} limpieza(s) hace {int} días")]
    public async Task GivenCompletedCleaningsDaysAgo(string name, int count, int daysAgo)
    {
        var now = world.Clock.Now;
        world.Clock.Now = now.AddDays(-daysAgo);
        await GivenCompletedCleanings(name, count);
        world.Clock.Now = now;
    }

    [Given("que {string} resolvió {int} incidencia(s)")]
    public async Task GivenResolvedIncidents(string name, int count)
    {
        for (var i = 0; i < count; i++) await Repair(name, wait: TimeSpan.FromMinutes(15), work: TimeSpan.FromMinutes(45));
    }

    [Given("que {string} limpió una habitación en {int} minutos después de esperar {int} minutos")]
    public Task GivenCleanedARoom(string name, int workMinutes, int waitMinutes) =>
        Clean(name, TimeSpan.FromMinutes(waitMinutes), TimeSpan.FromMinutes(workMinutes));

    [Given("que {string} resolvió una incidencia en {int} minutos después de esperar {int} minutos")]
    public Task GivenResolvedAnIncident(string name, int workMinutes, int waitMinutes) =>
        Repair(name, TimeSpan.FromMinutes(waitMinutes), TimeSpan.FromMinutes(workMinutes));

    // ── Reports ─────────────────────────────────────────────────────

    [When("consulto el reporte de productividad")]
    public async Task WhenIQueryTheProductivityReport()
    {
        var (from, to) = LastDays();
        _productivity = await world.Request<IStaffPerformanceQueryService, ProductivityReport>(service =>
            service.Handle(new GetStaffProductivityQuery(world.HotelId, from, to)));
    }

    [When("consulto los indicadores de tiempos de atención")]
    public async Task WhenIQueryTheServiceTimes()
    {
        var (from, to) = LastDays();
        _serviceTimes = await world.Request<IStaffPerformanceQueryService, ServiceTimesReport>(service =>
            service.Handle(new GetServiceTimesQuery(world.HotelId, from, to)));
    }

    // ── US-24 ───────────────────────────────────────────────────────

    [Then("el reporte muestra que {string} completó {int} tarea(s)")]
    public void ThenTheReportShowsCompleted(string name, int count)
    {
        var collaborator = _productivity!.Collaborators.Should().ContainSingle(c => c.UserId == Id(name)).Subject;
        collaborator.TotalCompleted.Should().Be(count);
        collaborator.FullName.Should().Be(name);
    }

    [Then("{string} aparece primero en el reporte")]
    public void ThenAppearsFirst(string name) => _productivity!.Collaborators[0].UserId.Should().Be(Id(name));

    [Then("{string} no aparece en el reporte")]
    public void ThenDoesNotAppear(string name) =>
        _productivity!.Collaborators.Should().NotContain(c => c.UserId == Id(name));

    [Then("el área de {word} completó {int} tarea(s) con {int} colaborador(es)")]
    public void ThenTheAreaCompleted(string area, int completed, int collaborators)
    {
        var row = Area(area);
        row.Completed.Should().Be(completed);
        row.Collaborators.Should().Be(collaborators);
        row.AveragePerCollaborator.Should().Be(Math.Round((double)completed / collaborators, 1));
    }

    [Then("el área de {word} aparece sin tareas completadas")]
    public void ThenTheAreaHasNothing(string area)
    {
        var row = Area(area);
        row.Completed.Should().Be(0);
        row.Collaborators.Should().Be(0);
    }

    // ── US-25 ───────────────────────────────────────────────────────

    [Then("limpiar una habitación demora en promedio {int} minutos")]
    public void ThenCleaningTakes(int minutes) => _serviceTimes!.Cleaning.AverageWorkMinutes.Should().Be(minutes);

    [Then("una limpieza espera en promedio {int} minutos hasta iniciarse")]
    public void ThenCleaningWaits(int minutes) => _serviceTimes!.Cleaning.AverageResponseMinutes.Should().Be(minutes);

    [Then("una limpieza tarda en promedio {int} minutos en total")]
    public void ThenCleaningTotal(int minutes) => _serviceTimes!.Cleaning.AverageTotalMinutes.Should().Be(minutes);

    [Then("resolver una incidencia demora en promedio {int} minutos")]
    public void ThenAnIncidentTakes(int minutes) => _serviceTimes!.Incidents.AverageWorkMinutes.Should().Be(minutes);

    [Then("una incidencia tarda en promedio {int} minutos en total")]
    public void ThenAnIncidentTotal(int minutes) => _serviceTimes!.Incidents.AverageTotalMinutes.Should().Be(minutes);

    [Then("no hay limpiezas ni incidencias medidas ni promedios")]
    public void ThenNothingWasMeasured()
    {
        foreach (var indicator in new[] { _serviceTimes!.Cleaning, _serviceTimes.Incidents })
        {
            indicator.Count.Should().Be(0);
            indicator.AverageResponseMinutes.Should().BeNull();
            indicator.AverageWorkMinutes.Should().BeNull();
            indicator.AverageTotalMinutes.Should().BeNull();
        }
    }

    // ── US-34 ───────────────────────────────────────────────────────

    [When("descargo el reporte de productividad en CSV")]
    public async Task WhenIDownloadTheProductivityCsv()
    {
        var (from, to) = LastDays();
        var file = await world.Request<IStaffPerformanceQueryService, IActionResult>(service =>
        {
            var controller = new StaffPerformanceController(service, world.Clock) { ControllerContext = AdministratorContext() };
            return controller.ExportStaffProductivity(null, from, to);
        });
        var download = file.Should().BeOfType<FileContentResult>().Subject;
        download.ContentType.Should().StartWith("text/csv");
        download.FileDownloadName.Should().EndWith(".csv");
        _csv = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(download.FileContents).TrimStart('\uFEFF');
    }

    [Then("el archivo tiene una fila de {string} con {int} limpiezas")]
    public void ThenTheFileHasARowOf(string name, int cleanings)
    {
        var lines = _csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var header = lines[0].Split(',');
        var row = lines.Skip(1).Select(line => line.Split(',')).Single(cells => cells[Array.IndexOf(header, "nombre")] == name);
        row[Array.IndexOf(header, "tipo")].Should().Be("colaborador");
        row[Array.IndexOf(header, "limpiezas")].Should().Be(cleanings.ToString());
    }

    [Given("que programé el envío semanal de los reportes a {string}")]
    public async Task GivenIScheduledTheWeeklyReports(string recipient)
    {
        _schedule = await world.Request<IReportScheduleCommandService, ReportSchedule>(service => service.Handle(
            new CreateReportScheduleCommand(world.HotelId, new ReportScheduleSettings(ReportFrequency.Weekly, [recipient]),
                CreatedByUserId: 1)));
        _scheduledRun = _schedule.NextRunAt;
    }

    [When("llega la hora del siguiente envío y se ejecuta el programador")]
    public async Task WhenTheSchedulerRunsOnTime()
    {
        world.Clock.Now = _scheduledRun.AddMinutes(5);
        await RunTheScheduler();
    }

    [When("se ejecuta el programador antes de la hora del envío")]
    public async Task WhenTheSchedulerRunsEarly()
    {
        world.Clock.Now = _scheduledRun.AddMinutes(-1);
        await RunTheScheduler();
    }

    [Then("{string} recibe el reporte semanal con el trabajo de {string}")]
    public void ThenTheRecipientGetsTheReport(string recipient, string name)
    {
        _sent.Should().Be(1);
        var email = world.Emails.Sent.Should().ContainSingle(message => message.To == recipient).Subject;
        email.Subject.Should().Contain("semanal");
        email.TextBody.Should().Contain($"{name}: 2 tareas");
        email.TextBody.Should().Contain("Limpieza de habitaciones");
    }

    [Then("el siguiente envío queda para una semana después")]
    public void ThenTheNextRunIsAWeekLater()
    {
        var stored = world.Read(context => context.Set<ReportSchedule>().Single());
        stored.NextRunAt.Should().Be(_scheduledRun.AddDays(7));
        stored.LastSentAt.Should().Be(world.Clock.Now);
    }

    [Then("no se envía ningún reporte")]
    public void ThenNothingIsSent()
    {
        _sent.Should().Be(0);
        world.Emails.Sent.Should().NotContain(message => message.Subject.StartsWith("Reporte"));
        world.Read(context => context.Set<ReportSchedule>().Single()).NextRunAt.Should().Be(_scheduledRun);
    }

    private async Task RunTheScheduler() =>
        _sent = await world.Request<IReportScheduleCommandService, int>(service => service.Handle(new SendDueReportsCommand()));

    /// <summary>A request of the administrator of the hotel.</summary>
    private ControllerContext AdministratorContext() => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(IamClaimTypes.UserId, "1"),
                new Claim(IamClaimTypes.HotelId, world.HotelId.ToString()),
                new Claim(ClaimTypes.Role, UserRoles.Admin)
            ], authenticationType: "Test"))
        }
    };

    // ── Support ─────────────────────────────────────────────────────

    private int Id(string name) => world.Iam.Staff.Single(contact => contact.FullName == name).UserId;

    private string Email(string name) => world.Iam.Staff.Single(contact => contact.FullName == name).Email;

    private AreaProductivity Area(string area) => _productivity!.Areas.Single(row => row.Area == Areas[area]);

    /// <summary>The report of the last 30 days, read a minute after the last change (the end is exclusive).</summary>
    private (DateTimeOffset From, DateTimeOffset To) LastDays()
    {
        world.Clock.Now = world.Clock.Now.AddMinutes(1);
        return (world.Clock.Now.AddDays(-PeriodDays), world.Clock.Now);
    }

    /// <summary>The rooms of the hotel in turn, so no room has two open tasks at once.</summary>
    private int NextRoom()
    {
        var rooms = world.Rooms.Values.ToList();
        return rooms[_nextRoom++ % rooms.Count];
    }

    /// <summary>Reception creates a cleaning task for <paramref name="name"/>, who starts it after <paramref name="wait"/> and completes it after <paramref name="work"/>.</summary>
    private async Task Clean(string name, TimeSpan wait, TimeSpan work)
    {
        var housekeeper = new CleaningTaskActor(Id(name), Email(name), CleaningTaskRole.Housekeeper);
        var task = await world.Request<ICleaningTaskCommandService, CleaningTask>(service => service.Handle(
            new CreateCleaningTaskCommand(NextRoom(), CleaningTaskPriority.Normal, null, Id(name),
                new CleaningTaskActor(ReceptionUserId, "recepcion@roomtrack.pe", CleaningTaskRole.FrontDesk))));
        world.Clock.Now += wait;
        await world.Request<ICleaningTaskCommandService, CleaningTask?>(service =>
            service.Handle(new StartCleaningTaskCommand(task.Id, housekeeper)));
        world.Clock.Now += work;
        var completed = await world.Request<ICleaningTaskCommandService, CleaningTask?>(service =>
            service.Handle(new CompleteCleaningTaskCommand(task.Id, housekeeper)));
        completed!.Status.Should().Be(CleaningTaskStatus.Completed);
    }

    /// <summary>Reception reports an incident; <paramref name="name"/> starts it after <paramref name="wait"/> and resolves it after <paramref name="work"/>.</summary>
    private async Task Repair(string name, TimeSpan wait, TimeSpan work)
    {
        var maintenance = new IncidentActor(Id(name), Email(name), IsAdministrator: false);
        var incident = await world.Request<IIncidentCommandService, Incident>(service => service.Handle(
            new ReportIncidentCommand(NextRoom(), "Ducha sin agua caliente", null, IncidentPriority.Medium, false,
                new IncidentActor(ReceptionUserId, "recepcion@roomtrack.pe", IsAdministrator: false))));
        world.Clock.Now += wait;
        await world.Request<IIncidentCommandService, Incident?>(service =>
            service.Handle(new StartIncidentCommand(incident.Id, maintenance)));
        world.Clock.Now += work;
        var resolved = await world.Request<IIncidentCommandService, Incident?>(service =>
            service.Handle(new ResolveIncidentCommand(incident.Id, "Resistencia cambiada", maintenance)));
        resolved!.Status.Should().Be(IncidentStatus.Resolved);
    }
}
