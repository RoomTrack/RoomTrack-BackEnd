using System;
using System.Linq;
using FluentAssertions;
using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Exceptions;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Xunit;

namespace RoomTrack.Tests.Housekeeping.Domain;

/// <summary>Rules of the cleaning checklist (US-07).</summary>
public class CleaningChecklistTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly CleaningTaskActor Reception = new(10, "recepcion@roomtrack.pe", CleaningTaskRole.FrontDesk);
    private static readonly CleaningTaskActor Rosa = new(30, "rosa@roomtrack.pe", CleaningTaskRole.Housekeeper);
    private static readonly CleaningTaskActor Beto = new(31, "beto@roomtrack.pe", CleaningTaskRole.Housekeeper);

    private static CleaningTask StartedTask()
    {
        var task = CleaningTask.Create(1, 501, CleaningTaskPriority.Normal, null, Reception, Now,
            [new("Cambiar ropa de cama"), new("Reponer frutas", IsRequired: false)]);
        task.AssignTo(Rosa.UserId, Reception, Now);
        task.Start(Rosa, Now);
        return task;
    }

    [Fact]
    public void OptionalSteps_ShouldNotBlockTheCompletion()
    {
        var task = StartedTask();
        task.MarkChecklistItem(Item(task, "Cambiar ropa de cama"), done: true, Rosa, Now);

        task.Complete(Rosa, Now).Should().BeTrue();
        task.ChecklistProgress.Should().Be(50);
    }

    [Fact]
    public void UncheckingAStep_ShouldBlockTheCompletionAgain()
    {
        var task = StartedTask();
        var step = Item(task, "Cambiar ropa de cama");
        task.MarkChecklistItem(step, done: true, Rosa, Now);
        task.MarkChecklistItem(step, done: false, Rosa, Now);

        var complete = () => task.Complete(Rosa, Now);

        complete.Should().Throw<CleaningChecklistIncompleteException>();
        task.History.Count(entry => entry.Action == CleaningTaskHistoryAction.ChecklistItemUnchecked).Should().Be(1);
    }

    [Fact]
    public void OnlyTheAssignee_ShouldMarkTheSteps()
    {
        var task = StartedTask();

        var mark = () => task.MarkChecklistItem(Item(task, "Cambiar ropa de cama"), done: true, Beto, Now);

        mark.Should().Throw<NotCleaningTaskAssigneeException>();
    }

    [Fact]
    public void ATemplateWithRepeatedSteps_ShouldBeRefused()
    {
        var template = new CleaningChecklistTemplate(1, 3);

        var configure = () => template.Configure([new("Cambiar sábanas"), new("cambiar sábanas")], 1, Now);

        configure.Should().Throw<InvalidFieldException>().Which.Violation.Code.Should().Be(CleaningTaskErrorCodes.ChecklistItemDuplicated);
    }

    private static long Item(CleaningTask task, string label) => task.Checklist.Single(item => item.Label == label).Id;
}
