using System;
using System.Linq;
using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Events;
using RoomTrack.Housekeeping.Domain.Model.Exceptions;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;
using FluentAssertions;
using Xunit;

namespace RoomTrack.Tests.Housekeeping.Domain;

public class CleaningTaskTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly CleaningTaskActor Reception = new(UserId: 10, "recepcion@roomtrack.pe", CleaningTaskRole.FrontDesk);
    private static readonly CleaningTaskActor Housekeeper = new(UserId: 30, "limpieza@roomtrack.pe", CleaningTaskRole.Housekeeper);
    private static readonly CleaningTaskActor OtherHousekeeper = new(UserId: 31, "limpieza2@roomtrack.pe", CleaningTaskRole.Housekeeper);
    private static readonly CleaningTaskActor Admin = new(UserId: 1, "admin@roomtrack.pe", CleaningTaskRole.Administrator);

    private static CleaningTask Create(CleaningTaskPriority priority = CleaningTaskPriority.Normal) =>
        CleaningTask.Create(hotelId: 1, roomId: 12, priority, "  Extra bed  ", Reception, Now);

    private static CleaningTask AssignedTo(CleaningTaskActor housekeeper)
    {
        var task = Create();
        task.AssignTo(housekeeper.UserId, Reception, Now);
        return task;
    }

    [Fact]
    public void Create_ShouldStartPendingUnassigned_WithTrimmedNotes_AndCreatedHistory()
    {
        var task = Create(CleaningTaskPriority.High);

        task.Status.Should().Be(CleaningTaskStatus.Pending);
        task.Priority.Should().Be(CleaningTaskPriority.High);
        task.Notes.Should().Be("Extra bed");
        task.AssigneeUserId.Should().BeNull();
        task.CreatedByUserId.Should().Be(Reception.UserId);
        task.History.Should().ContainSingle().Which.Action.Should().Be(CleaningTaskHistoryAction.Created);
    }

    [Fact]
    public void Create_ByHousekeeping_ShouldBeRefused()
    {
        var act = () => CleaningTask.Create(1, 12, CleaningTaskPriority.Normal, null, Housekeeper, Now);

        act.Should().Throw<CleaningTaskManageNotAllowedException>();
    }

    [Fact]
    public void CreateForRoomToClean_ShouldBeASystemTaskWithNormalPriority()
    {
        var task = CleaningTask.CreateForRoomToClean(hotelId: 1, roomId: 12, Now);

        task.CreatedByUserId.Should().BeNull();
        task.Priority.Should().Be(CleaningTaskPriority.Normal);
        task.History.Single().ActorUserId.Should().BeNull();
    }

    [Fact]
    public void Assign_ByTheFrontDesk_ShouldPutItInTheAssigneesList_AndRaiseTheEventOnceTheTaskHasItsId()
    {
        var task = Create();

        task.AssignTo(Housekeeper.UserId, Reception, Now);

        task.AssigneeUserId.Should().Be(Housekeeper.UserId);
        task.AssignedAutomatically.Should().BeFalse();
        // Not saved yet: the event waits for the generated id.
        task.DomainEvents.Should().BeEmpty();
        SetId(task, 5);
        task.DomainEvents.OfType<CleaningTaskAssignedEvent>().Should().ContainSingle()
            .Which.Should().Match<CleaningTaskAssignedEvent>(e => e.CleaningTaskId == 5 && e.AssigneeUserId == Housekeeper.UserId
                                                                  && e.PreviousAssigneeUserId == null);
    }

    [Fact]
    public void Reassign_ShouldMoveTheTaskToTheNewAssignee_AndRecordWhoHadIt()
    {
        var task = AssignedTo(Housekeeper);
        SetId(task, 5);
        task.ClearDomainEvents();

        task.AssignTo(OtherHousekeeper.UserId, Reception, Now);

        task.AssigneeUserId.Should().Be(OtherHousekeeper.UserId);
        task.History.Last().Action.Should().Be(CleaningTaskHistoryAction.Reassigned);
        task.DomainEvents.OfType<CleaningTaskAssignedEvent>().Single().PreviousAssigneeUserId.Should().Be(Housekeeper.UserId);
    }

    [Fact]
    public void Housekeeping_ShouldOnlyTakeAnUnassignedTaskForThemselves()
    {
        var unassigned = Create();
        unassigned.AssignTo(Housekeeper.UserId, Housekeeper, Now);
        unassigned.AssigneeUserId.Should().Be(Housekeeper.UserId);

        var takeFromOther = () => AssignedTo(OtherHousekeeper).AssignTo(Housekeeper.UserId, Housekeeper, Now);
        var giveToOther = () => Create().AssignTo(OtherHousekeeper.UserId, Housekeeper, Now);

        takeFromOther.Should().Throw<OperationNotAllowedException>();
        giveToOther.Should().Throw<OperationNotAllowedException>();
    }

    [Fact]
    public void Start_ShouldRequireAnAssignee_AndOnlyTheAssigneeOrAnAdministrator()
    {
        var unassigned = () => Create().Start(Admin, Now);
        var byOther = () => AssignedTo(Housekeeper).Start(OtherHousekeeper, Now);

        unassigned.Should().Throw<CleaningTaskNotAssignedException>();
        byOther.Should().Throw<NotCleaningTaskAssigneeException>();
        AssignedTo(Housekeeper).Start(Admin, Now).Should().BeTrue();
    }

    [Fact]
    public void StartAndComplete_ShouldBeIdempotent_SoAnOfflineQueueCanBeReplayed()
    {
        var task = AssignedTo(Housekeeper);

        task.Start(Housekeeper, Now).Should().BeTrue();
        task.Start(Housekeeper, Now.AddMinutes(1)).Should().BeFalse();
        task.StartedAt.Should().Be(Now);

        task.Complete(Housekeeper, Now.AddMinutes(30)).Should().BeTrue();
        task.Complete(Housekeeper, Now.AddMinutes(31)).Should().BeFalse();
        task.Status.Should().Be(CleaningTaskStatus.Completed);
        task.CompletedAt.Should().Be(Now.AddMinutes(30));
        task.History.Count(entry => entry.Action == CleaningTaskHistoryAction.Started).Should().Be(1);
        task.History.Count(entry => entry.Action == CleaningTaskHistoryAction.Completed).Should().Be(1);
    }

    [Fact]
    public void Complete_APendingTask_ShouldFail()
    {
        var act = () => AssignedTo(Housekeeper).Complete(Housekeeper, Now);

        act.Should().Throw<InvalidCleaningTaskStatusTransitionException>();
    }

    [Fact]
    public void Complete_ShouldRaiseTheCompletedEvent()
    {
        var task = AssignedTo(Housekeeper);
        SetId(task, 5);
        task.Start(Housekeeper, Now);
        task.ClearDomainEvents();

        task.Complete(Housekeeper, Now);

        task.DomainEvents.OfType<CleaningTaskCompletedEvent>().Should().ContainSingle()
            .Which.CompletedByUserId.Should().Be(Housekeeper.UserId);
    }

    [Fact]
    public void RecordImpediment_ShouldKeepTheIncidentInTheTask()
    {
        var task = AssignedTo(Housekeeper);
        task.Start(Housekeeper, Now);

        task.RecordImpediment(incidentId: 42, "Broken mirror", Housekeeper, Now);

        task.Status.Should().Be(CleaningTaskStatus.InProgress);
        task.ImpedimentIncidentIds.Should().Equal(42);
        task.History.Last().Should().Match<RoomTrack.Housekeeping.Domain.Model.Entities.CleaningTaskHistoryEntry>(
            entry => entry.Action == CleaningTaskHistoryAction.ImpedimentReported && entry.Detail == "Broken mirror");
    }

    [Fact]
    public void ClosedTasks_ShouldRefuseEveryChange()
    {
        var task = AssignedTo(Housekeeper);
        task.Cancel("Wrong room", Reception, Now);

        var assign = () => task.AssignTo(OtherHousekeeper.UserId, Reception, Now);
        var start = () => task.Start(Housekeeper, Now);
        var cancel = () => task.Cancel(null, Reception, Now);

        task.Status.Should().Be(CleaningTaskStatus.Cancelled);
        task.CancellationReason.Should().Be("Wrong room");
        assign.Should().Throw<CleaningTaskClosedException>();
        start.Should().Throw<CleaningTaskClosedException>();
        cancel.Should().Throw<CleaningTaskClosedException>();
    }

    [Fact]
    public void ChangePriorityAndCancel_ShouldBeReservedToTheFrontDesk()
    {
        var task = AssignedTo(Housekeeper);

        var reprioritize = () => task.ChangePriority(CleaningTaskPriority.Urgent, Housekeeper, Now);
        var cancel = () => task.Cancel(null, Housekeeper, Now);

        reprioritize.Should().Throw<CleaningTaskManageNotAllowedException>();
        cancel.Should().Throw<CleaningTaskManageNotAllowedException>();
        task.ChangePriority(CleaningTaskPriority.Urgent, Reception, Now);
        task.Priority.Should().Be(CleaningTaskPriority.Urgent);
    }

    [Fact]
    public void Notes_ShouldNotExceedTheirMaximumLength()
    {
        var act = () => CleaningTask.Create(1, 12, CleaningTaskPriority.Normal, new string('x', CleaningTask.NotesMaxLength + 1), Reception, Now);

        (act.Should().Throw<InvalidFieldException>()).Which.Violation.Code.Should().Be(CleaningTaskErrorCodes.NotesLength);
    }

    private static void SetId(CleaningTask task, int id) =>
        typeof(CleaningTask).GetProperty(nameof(CleaningTask.Id))!.SetValue(task, id);
}
