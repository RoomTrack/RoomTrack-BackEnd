using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using RoomTrack.Profiles.API.Infrastructure.Persistence;
using RoomTrack.Profiles.Domain.Model.Aggregates;
using RoomTrack.Profiles.Domain.Model.Enums;
using RoomTrack.Profiles.Domain.Model.ValueObjects;
using RoomTrack.Profiles.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Repositories;
using RoomTrack.Shifts.Application;
using RoomTrack.Shifts.Domain;
using RoomTrack.Shifts.Infrastructure;

namespace RoomTrack.Profiles.API.Tests.Features.Steps;

/// <summary>
///     Steps of US-31 through <see cref="ShiftService"/>, the real repositories of the Profiles service and the staff
///     profiles of the hotel, on an in-memory database (each request with its own context, as in the service).
/// </summary>
[Binding]
public sealed class ShiftSteps : IDisposable
{
    private const int HotelId = 1;
    private const int AdministratorUserId = 1;
    private static readonly DateTimeOffset Now = new(2030, 11, 25, 15, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, StaffRole> Roles = new()
    {
        ["limpieza"] = StaffRole.Housekeeping,
        ["recepción"] = StaffRole.Reception,
        ["mantenimiento"] = StaffRole.Maintenance
    };

    private static readonly Dictionary<string, ShiftArea> Areas = new()
    {
        ["limpieza"] = ShiftArea.Housekeeping,
        ["recepción"] = ShiftArea.FrontDesk,
        ["mantenimiento"] = ShiftArea.Maintenance
    };

    private static readonly Dictionary<string, ShiftPeriod> Periods = new()
    {
        ["mañana"] = ShiftPeriod.Morning,
        ["tarde"] = ShiftPeriod.Afternoon,
        ["noche"] = ShiftPeriod.Night
    };

    private readonly string _database = Guid.NewGuid().ToString();
    private readonly List<ProfilesDbContext> _contexts = [];
    private readonly Dictionary<string, int> _staff = [];
    private readonly Dictionary<(ShiftPeriod, ShiftArea), int> _shifts = [];
    private WorkShift? _shift;
    private ShiftSwapRequest? _swap;
    private IReadOnlyList<CoverageGap> _gaps = [];
    private DomainException? _error;

    public void Dispose() => _contexts.ForEach(context => context.Dispose());

    [Given("que el hotel tiene al personal:")]
    public async Task GivenTheStaff(DataTable people)
    {
        var context = NewContext();
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        var userId = 100;
        foreach (var row in people.Rows)
        {
            var name = row["nombre"];
            var staff = new StaffProfile(StaffProfileId.New(), new UserId(userId), new EmployeeCode($"EMP-{userId:D5}"),
                new PersonName(name, "Quispe"), new EmailAddress($"{name.ToLowerInvariant()}@roomtrack.pe"),
                new JobPosition("Operativo"), HabitualShift.Rotating);
            staff.AddAssignment(ScopeLevel.Hotel, new TargetId(HotelId), Roles[row["rol"]], new DateRange(today, null), today);
            context.Add(staff);
            _staff[name] = userId++;
        }
        await context.SaveChangesAsync();
    }

    [Given("que defino el turno {string} de {string} del {word} para {int} personas")]
    public async Task GivenIDefineAShift(string period, string area, string date, int people) =>
        _shift = await Define(period, area, date, people);

    [When("asigno a {string} y {string}")]
    public async Task WhenIAssign(string first, string second)
    {
        await Service().AssignAsync(_shift!.Id, _staff[first]);
        _shift = await Service().AssignAsync(_shift.Id, _staff[second]);
    }

    [Then("cada una ve su horario en su panel")]
    public async Task ThenEachOneSeesHerSchedule()
    {
        foreach (var name in new[] { "Rosa", "Ana" })
        {
            var mine = await Service().ScheduleOfAsync(_staff[name], Date("01/12/2030"), Date("07/12/2030"));
            mine.Should().ContainSingle().Which.Period.Should().Be(ShiftPeriod.Morning);
        }
        (await Service().ScheduleOfAsync(_staff["Carlos"], Date("01/12/2030"), Date("07/12/2030"))).Should().BeEmpty();
    }

    [Given("que el turno {string} de {string} del {word} necesita {int} persona y no tiene a nadie")]
    public Task GivenAnEmptyShift(string period, string area, string date, int people) => Define(period, area, date, people);

    [Given("el turno {string} de {string} del {word} necesita {int} personas y tiene a {string}")]
    public Task GivenAShiftWithOnePerson(string period, string area, string date, int people, string name) =>
        Define(period, area, date, people, _staff[name]);

    [When("reviso la planificación")]
    public async Task WhenIReviewThePlan() =>
        _gaps = await Service().CoverageGapsAsync(HotelId, Date("01/12/2030"), Date("01/12/2030"));

    [Then("el sistema resalta el hueco de cobertura de {string} primero")]
    public void ThenTheGapIsHighlightedFirst(string area)
    {
        var first = _gaps.First();
        first.Shift.Area.Should().Be(Areas[area]);
        first.IsCritical.Should().BeTrue();
        first.Missing.Should().Be(1);
    }

    [Then("señala que a {string} le faltan {int} personas")]
    public void ThenTheAreaMisses(string area, int missing) =>
        _gaps.Single(gap => gap.Shift.Area == Areas[area]).Missing.Should().Be(missing);

    [Given("que {string} trabaja el turno {string} de {string} del {word}")]
    [Given("{string} trabaja el turno {string} de {string} del {word}")]
    public async Task GivenWorksTheShift(string name, string period, string area, string date) =>
        _shifts[(Periods[period], Areas[area])] = (await Define(period, area, date, 1, _staff[name])).Id;

    [Given("{string} solicita cambiar su turno con {string}")]
    public async Task GivenRequestsASwap(string requester, string colleague) =>
        _swap = await Service().RequestSwapAsync(_staff[requester], _shifts[(ShiftPeriod.Morning, ShiftArea.Housekeeping)],
            _staff[colleague], _shifts[(ShiftPeriod.Afternoon, ShiftArea.Housekeeping)], "Cita médica en la mañana");

    [When("lo apruebo")]
    public async Task WhenIApprove() => _swap = await Service().ApproveSwapAsync(_swap!.Id, AdministratorUserId);

    [Then("el calendario se actualiza para ambas partes involucradas")]
    public async Task ThenBothCalendarsAreUpdated()
    {
        _swap!.Status.Should().Be(ShiftSwapStatus.Approved);
        _swap.DecidedByUserId.Should().Be(AdministratorUserId);
        (await Service().ScheduleOfAsync(_staff["Rosa"], Date("01/12/2030"), Date("01/12/2030")))
            .Should().ContainSingle().Which.Period.Should().Be(ShiftPeriod.Afternoon);
        (await Service().ScheduleOfAsync(_staff["Ana"], Date("01/12/2030"), Date("01/12/2030")))
            .Should().ContainSingle().Which.Period.Should().Be(ShiftPeriod.Morning);
    }

    [When("intento asignar a alguien que no trabaja en el hotel")]
    public async Task WhenITryToAssignAStranger()
    {
        try { await Service().AssignAsync(_shift!.Id, 999); }
        catch (DomainException e) { _error = e; }
    }

    [Then("el sistema rechaza la operación")]
    public void ThenTheSystemRefuses() =>
        _error.Should().BeOfType<InvalidFieldException>().Which.Violation.Code.Should().Be(ShiftErrorCodes.NotStaffOfHotel);

    private Task<WorkShift> Define(string period, string area, string date, int people, params int[] assignees) =>
        Service().DefineAsync(HotelId, Date(date), Periods[period], Areas[area], people, assignees, AdministratorUserId);

    private static DateOnly Date(string day) => DateOnly.ParseExact(day, "dd/MM/yyyy", null);

    private ProfilesDbContext NewContext()
    {
        var context = new ProfilesDbContext(new DbContextOptionsBuilder<ProfilesDbContext>().UseInMemoryDatabase(_database).Options);
        _contexts.Add(context);
        return context;
    }

    /// <summary>The shifts use case for one request, with its own context.</summary>
    private ShiftService Service()
    {
        var context = NewContext();
        return new ShiftService(new WorkShiftRepository(context), new ShiftSwapRequestRepository(context),
            new StaffProfileRepository(context), new SaveChangesUnitOfWork(context), new FixedClock());
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class SaveChangesUnitOfWork(ProfilesDbContext context) : IUnitOfWork
    {
        public Task CompleteAsync() => context.SaveChangesAsync();
        public Task ExecuteInTransactionAsync(Func<Task> work) => work();
    }
}
