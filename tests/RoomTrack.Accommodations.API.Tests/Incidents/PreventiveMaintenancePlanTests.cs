using System;
using System.Linq;
using FluentAssertions;
using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Events;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Xunit;

namespace RoomTrack.Tests.Incidents;

/// <summary>Rules of the preventive maintenance plans (US-30) and of the recurring incidents (US-10).</summary>
public class PreventiveMaintenancePlanTests
{
    private static readonly DateOnly Today = new(2030, 1, 10);
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 13, 0, 0, TimeSpan.Zero);

    private static PreventiveMaintenancePlan Plan(int frequency = 30, int reminder = 3) =>
        PreventiveMaintenancePlan.Schedule(1, 12, "Limpiar filtros", null, frequency, Today.AddDays(5), reminder, 20, Today, Now);

    [Fact]
    public void TheReminder_ShouldBeSentOncePerCycle()
    {
        var plan = Plan();

        plan.Remind(Today.AddDays(1), Now).Should().BeFalse("the window starts 3 days before");
        plan.Remind(Today.AddDays(2), Now).Should().BeTrue();
        plan.Remind(Today.AddDays(3), Now).Should().BeFalse("already reminded for this due date");
        plan.DomainEvents.OfType<PreventiveMaintenanceReminderDueEvent>().Should().ContainSingle();
    }

    [Fact]
    public void ALateRun_ShouldNotPileUpMissedCycles()
    {
        var plan = Plan(frequency: 7);

        plan.RecordGenerated(99, Today.AddDays(30), Now);

        plan.NextDueOn.Should().Be(Today.AddDays(33), "the first due date after the run");
        plan.GeneratedCount.Should().Be(1);
    }

    [Fact]
    public void ADeactivatedPlan_ShouldNotBeDue()
    {
        var plan = Plan();
        plan.Deactivate(Now);

        plan.IsDue(Today.AddDays(10)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(400, 3)]
    [InlineData(5, 5)]
    public void AnInvalidSchedule_ShouldBeRefused(int frequency, int reminder)
    {
        var schedule = () => Plan(frequency, reminder);

        schedule.Should().Throw<InvalidFieldException>();
    }

    [Fact]
    public void AFirstDueDateInThePast_ShouldBeRefused()
    {
        var schedule = () => PreventiveMaintenancePlan.Schedule(1, 12, "Limpiar filtros", null, 30, Today.AddDays(-1), 3, 20, Today, Now);

        schedule.Should().Throw<InvalidFieldException>().Which.Field.Should().Be("firstDueOn");
    }

    [Fact]
    public void AnIncident_ShouldBeFlaggedRecurring_OnlyAboveTheThreshold()
    {
        var reporter = new IncidentActor(10, "recepcion@roomtrack.pe", IsAdministrator: false);
        var third = Incident.Report(1, 12, "Aire no enfría", null, IncidentPriority.High, false, reporter, Now);
        var fourth = Incident.Report(1, 12, "Aire no enfría", null, IncidentPriority.High, false, reporter, Now);

        third.FlagRecurrence(3, threshold: 3, Now).Should().BeFalse();
        fourth.FlagRecurrence(4, threshold: 3, Now).Should().BeTrue();
        fourth.IsRecurring.Should().BeTrue();
        fourth.History.Should().Contain(entry => entry.Action == IncidentHistoryAction.RecurrenceDetected);
    }
}
