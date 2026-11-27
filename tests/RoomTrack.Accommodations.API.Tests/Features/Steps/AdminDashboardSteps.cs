using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Reqnroll;
using RoomTrack.Accommodations.API.Infrastructure.Persistence;
using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Dashboard.Application;
using RoomTrack.Dashboard.Domain.Model;
using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Commands;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Housekeeping.Domain.Services;
using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Commands;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Incidents.Domain.Services;
using RoomTrack.Tests.Features.Support;

namespace RoomTrack.Tests.Features.Steps;

/// <summary>
///     Steps of US-18 (administrator's panel) through the real use cases of the operational contexts and the panel's
///     query service. The front desk of today is the one the fake of the Bookings service reports.
/// </summary>
[Binding]
public sealed class AdminDashboardSteps(OperationsWorld world)
{
    private const int ReceptionUserId = 1;

    private AdminDashboard? _dashboard;
    private int _criticalIncidentId;

    [Given("que la habitación {int} está ocupada")]
    public Task GivenTheRoomIsOccupied(int number) =>
        world.Request<AccommodationsDbContext, int>(context =>
        {
            context.Set<Room>().Single(room => room.Id == world.Rooms[number]).OccupyForCheckIn(900, "huesped@example.com", world.Clock.Now);
            return context.SaveChangesAsync();
        });

    [Given("que {string} está limpiando la habitación {int}")]
    public async Task GivenIsCleaning(string name, int number)
    {
        var housekeeper = new CleaningTaskActor(Id(name), Email(name), CleaningTaskRole.Housekeeper);
        var task = await world.Request<ICleaningTaskCommandService, CleaningTask>(service => service.Handle(
            new CreateCleaningTaskCommand(world.Rooms[number], CleaningTaskPriority.Normal, null, Id(name),
                new CleaningTaskActor(ReceptionUserId, "recepcion@roomtrack.pe", CleaningTaskRole.FrontDesk))));
        await world.Request<ICleaningTaskCommandService, CleaningTask?>(service =>
            service.Handle(new StartCleaningTaskCommand(task.Id, housekeeper)));
    }

    [Given("que recepción reportó una incidencia crítica en la habitación {int}")]
    public async Task GivenACriticalIncident(int number) =>
        _criticalIncidentId = (await world.Request<IIncidentCommandService, Incident>(service => service.Handle(
            new ReportIncidentCommand(world.Rooms[number], "Fuga de agua en el baño", null, IncidentPriority.Critical, false,
                new IncidentActor(ReceptionUserId, "recepcion@roomtrack.pe", IsAdministrator: false))))).Id;

    [Given("que hoy hay {int} llegadas, {int} huéspedes alojados y {int} salidas con promedio de evaluaciones {word}")]
    public void GivenTheFrontDeskToday(int arrivals, int inHouse, int departures, string average) =>
        world.Reservations.FrontDesk = new FrontDeskSummary(arrivals, inHouse, departures, 2,
            double.Parse(average, CultureInfo.InvariantCulture), []);

    [Given("que el servicio de reservas no responde")]
    public void GivenBookingsIsDown() => world.Reservations.FrontDesk = null;

    [When("consulto el panel del administrador")]
    public async Task WhenIOpenThePanel() =>
        _dashboard = await world.Request<IAdminDashboardQueryService, AdminDashboard>(service =>
            service.Handle(new GetAdminDashboardQuery(world.HotelId, TimeSpan.FromMinutes(60))));

    [When("pasan {int} minutos y consulto el panel del administrador")]
    public Task WhenMinutesPassAndIOpenThePanel(int minutes)
    {
        world.Clock.Now = world.Clock.Now.AddMinutes(minutes);
        return WhenIOpenThePanel();
    }

    [Then("el panel muestra {int} % de ocupación con {int} habitación ocupada y {int} en limpieza")]
    public void ThenTheRooms(int occupancy, int occupied, int cleaning)
    {
        _dashboard!.Rooms.OccupancyPercent.Should().Be(occupancy);
        _dashboard.Rooms.Occupied.Should().Be(occupied);
        _dashboard.Rooms.Cleaning.Should().Be(cleaning);
        _dashboard.Rooms.Total.Should().Be(world.Rooms.Count);
    }

    [Then("el panel muestra {int} tarea(s) de limpieza abierta(s)")]
    public void ThenTheOpenCleaningTasks(int open) => _dashboard!.Cleaning.Open.Should().Be(open);

    [Then("el panel muestra {int} tarea(s) de limpieza atrasada(s)")]
    public void ThenTheOverdueCleaningTasks(int overdue)
    {
        _dashboard!.Cleaning.Overdue.Should().Be(overdue);
        _dashboard.Cleaning.OverdueAfterMinutes.Should().Be(60);
    }

    [Then("el panel muestra {int} incidencia(s) abierta(s) y {int} crítica(s)")]
    public void ThenTheIncidents(int open, int critical)
    {
        _dashboard!.Incidents.Open.Should().Be(open);
        _dashboard.Incidents.Critical.Should().Be(critical);
    }

    [Then("la incidencia crítica de la habitación {int} encabeza lo que requiere atención")]
    public void ThenTheCriticalIncidentComesFirst(int number)
    {
        var first = _dashboard!.Attention.Should().NotBeEmpty().And.Subject.First();
        (first.Kind, first.ReferenceId, first.RoomNumber, first.Reason)
            .Should().Be(("Incident", _criticalIncidentId, number.ToString(), "Critical"));
    }

    [Then("el panel muestra {int} llegadas, {int} huéspedes alojados y {int} salidas")]
    public void ThenTheFrontDesk(int arrivals, int inHouse, int departures)
    {
        var frontDesk = _dashboard!.FrontDesk;
        frontDesk.Should().NotBeNull();
        (frontDesk!.ArrivalsToday, frontDesk.InHouse, frontDesk.DeparturesToday).Should().Be((arrivals, inHouse, departures));
    }

    [Then("el panel muestra el promedio de evaluaciones {word}")]
    public void ThenTheAverageRating(string average) =>
        _dashboard!.FrontDesk!.AverageRatingLast30Days.Should().Be(double.Parse(average, CultureInfo.InvariantCulture));

    [Then("el panel indica que la recepción no está disponible")]
    public void ThenTheFrontDeskIsUnavailable() => _dashboard!.FrontDesk.Should().BeNull();

    private int Id(string name) => world.Iam.Staff.Single(contact => contact.FullName == name).UserId;

    private string Email(string name) => world.Iam.Staff.Single(contact => contact.FullName == name).Email;
}
