using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Reqnroll;
using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Tests.Features.Support;

namespace RoomTrack.Tests.Features.Steps;

/// <summary>Steps of US-05 and US-06 on the CleaningTask aggregate.</summary>
[Binding]
public class HousekeepingSteps(ScenarioOutcome outcome)
{
    private const int HotelId = 1;
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static readonly CleaningTaskActor Reception = new(10, "recepcion@roomtrack.pe", CleaningTaskRole.FrontDesk);

    private static readonly Dictionary<string, CleaningTaskActor> Staff = new()
    {
        ["Lucía"] = new(30, "lucia@roomtrack.pe", CleaningTaskRole.Housekeeper),
        ["Mario"] = new(31, "mario@roomtrack.pe", CleaningTaskRole.Housekeeper)
    };

    private static readonly Dictionary<string, CleaningTaskStatus> Statuses = new()
    {
        ["pendiente"] = CleaningTaskStatus.Pending,
        ["en curso"] = CleaningTaskStatus.InProgress,
        ["completada"] = CleaningTaskStatus.Completed,
        ["cancelada"] = CleaningTaskStatus.Cancelled
    };

    private static readonly Dictionary<string, CleaningTaskHistoryAction> Actions = new()
    {
        ["asignación"] = CleaningTaskHistoryAction.Assigned,
        ["reasignación"] = CleaningTaskHistoryAction.Reassigned
    };

    private static readonly Dictionary<string, CleaningTaskPriority> Priorities = new()
    {
        ["baja"] = CleaningTaskPriority.Low,
        ["normal"] = CleaningTaskPriority.Normal,
        ["alta"] = CleaningTaskPriority.High,
        ["urgente"] = CleaningTaskPriority.Urgent
    };

    private CleaningTask _task = null!;

    [Given("una tarea de limpieza pendiente para la habitación {int}")]
    public void GivenAPendingCleaningTask(int roomId) =>
        _task = CleaningTask.Create(HotelId, roomId, CleaningTaskPriority.Normal, null, Reception, Now);

    [Given("que la tarea está asignada a {string}")]
    public void GivenTheTaskIsAssignedTo(string name) => _task.AssignTo(Staff[name].UserId, Reception, Now);

    [Given("que {string} inició la tarea")]
    [When("{string} inicia la tarea")]
    public void WhenStarts(string name) => _task.Start(Staff[name], Now.AddMinutes(5));

    [When("recepción asigna la tarea a {string}")]
    public void WhenReceptionAssigns(string name) => _task.AssignTo(Staff[name].UserId, Reception, Now);

    [When("{string} asigna la tarea a {string}")]
    public void WhenStaffAssigns(string actor, string assignee) => _task.AssignTo(Staff[assignee].UserId, Staff[actor], Now);

    [When("{string} intenta asignar la tarea a {string}")]
    public void WhenStaffTriesToAssign(string actor, string assignee) =>
        outcome.Capture(() => _task.AssignTo(Staff[assignee].UserId, Staff[actor], Now));

    [When("recepción cambia la prioridad de la tarea a {string}")]
    public void WhenReceptionChangesThePriority(string priority) => _task.ChangePriority(Priorities[priority], Reception, Now);

    [When("{string} finaliza la tarea")]
    public void WhenCompletes(string name) => _task.Complete(Staff[name], Now.AddMinutes(40));

    [When("{string} intenta iniciar la tarea")]
    public void WhenTriesToStart(string name) => outcome.Capture(() => _task.Start(Staff[name], Now));

    [When("{string} intenta finalizar la tarea")]
    public void WhenTriesToComplete(string name) => outcome.Capture(() => _task.Complete(Staff[name], Now));

    [When("{string} reporta el impedimento {string} con la incidencia {int}")]
    public void WhenReportsAnImpediment(string name, string title, int incidentId) =>
        _task.RecordImpediment(incidentId, title, Staff[name], Now.AddMinutes(10));

    [When("recepción cancela la tarea por {string}")]
    public void WhenReceptionCancels(string reason) => _task.Cancel(reason, Reception, Now);

    [Then("la tarea queda asignada a {string}")]
    public void ThenTheTaskIsAssignedTo(string name) => _task.AssigneeUserId.Should().Be(Staff[name].UserId);

    [Then("la tarea sigue sin persona asignada")]
    public void ThenTheTaskIsStillUnassigned() => _task.AssigneeUserId.Should().BeNull();

    [Then("el historial de la tarea registra la {string}")]
    public void ThenTheHistoryRecords(string action) => _task.History.Last().Action.Should().Be(Actions[action]);

    [Then("la tarea tiene prioridad {string}")]
    public void ThenTheTaskHasPriority(string priority) => _task.Priority.Should().Be(Priorities[priority]);

    [Then("la tarea queda {string}")]
    public void ThenTheTaskIs(string status) => _task.Status.Should().Be(Statuses[status]);

    [Then("la tarea registra que la completó {string}")]
    public void ThenTheTaskWasCompletedBy(string name)
    {
        _task.CompletedByUserId.Should().Be(Staff[name].UserId);
        _task.CompletedAt.Should().Be(Now.AddMinutes(40));
    }

    [Then("la tarea registra la incidencia {int}")]
    public void ThenTheTaskRecordsTheIncident(int incidentId) => _task.ImpedimentIncidentIds.Should().Contain(incidentId);

    [Then("{string} ya no puede iniciar la tarea")]
    public void ThenCannotStartAnyMore(string name)
    {
        outcome.Capture(() => _task.Start(Staff[name], Now));
        outcome.Error.Should().NotBeNull();
    }
}
