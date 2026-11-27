using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using RoomTrack.StaffPerformance.Domain.Model.ValueObjects;
using RoomTrack.StaffPerformance.Domain.Services;
using Xunit;

namespace RoomTrack.Tests.StaffPerformance;

/// <summary>Rules of the staff productivity report (US-24) and of the service-time indicators (US-25).</summary>
public class StaffPerformanceCalculatorTests
{
    private static readonly DateTimeOffset From = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = From.AddDays(30);
    private static readonly DateTimeOffset T = From.AddDays(5);

    private static readonly Dictionary<int, StaffMember> Staff = new()
    {
        [7] = new StaffMember(7, "Rosa", "rosa@roomtrack.pe", "Housekeeping"),
        [20] = new StaffMember(20, "Jorge", "jorge@roomtrack.pe", "Maintenance")
    };

    private static FinishedWork Work(WorkKind kind, WorkArea area, int? userId, int waitMinutes, int? workMinutes,
        bool countsForServiceTimes = true)
    {
        var started = workMinutes is null ? (DateTimeOffset?)null : T.AddMinutes(waitMinutes);
        var finished = T.AddMinutes(waitMinutes + (workMinutes ?? 0));
        return new FinishedWork(kind, 1, area, userId, T, started, finished, countsForServiceTimes);
    }

    [Fact]
    public void Productivity_ShouldCountTheWorkOfEachCollaborator_MostProductiveFirst()
    {
        var work = new[]
        {
            Work(WorkKind.Incident, WorkArea.Maintenance, 20, 10, 40),
            Work(WorkKind.CleaningTask, WorkArea.Housekeeping, 7, 5, 30),
            Work(WorkKind.CleaningTask, WorkArea.Housekeeping, 7, 5, 50),
            Work(WorkKind.GuestRequest, WorkArea.Housekeeping, 7, 2, 10)
        };

        var report = StaffPerformanceCalculator.Productivity(1, From, To, work, Staff);

        report.TotalCompleted.Should().Be(4);
        report.Collaborators.Select(c => c.UserId).Should().Equal(7, 20);
        var rosa = report.Collaborators[0];
        (rosa.CleaningTasksCompleted, rosa.GuestRequestsResolved, rosa.IncidentsResolved, rosa.TotalCompleted)
            .Should().Be((2, 1, 0, 3));
        rosa.FullName.Should().Be("Rosa");
        rosa.AverageWorkMinutes.Should().Be(30);
    }

    [Fact]
    public void Productivity_ShouldCompareEveryArea_EvenTheOnesWithoutWork()
    {
        var work = new[]
        {
            Work(WorkKind.CleaningTask, WorkArea.Housekeeping, 7, 5, 30),
            Work(WorkKind.CleaningTask, WorkArea.Housekeeping, 8, 5, 30),
            Work(WorkKind.CleaningTask, WorkArea.Housekeeping, 8, 5, 30),
            Work(WorkKind.Incident, WorkArea.Maintenance, 20, 10, 40)
        };

        var report = StaffPerformanceCalculator.Productivity(1, From, To, work, Staff);

        report.Areas.Select(a => (a.Area, a.Completed, a.Collaborators, a.AveragePerCollaborator)).Should().Equal(
            (WorkArea.FrontDesk, 0, 0, 0d),
            (WorkArea.Housekeeping, 3, 2, 1.5),
            (WorkArea.Maintenance, 1, 1, 1d));
    }

    [Fact]
    public void Productivity_ShouldKeepCollaboratorsIdentityDoesNotKnow_WithoutName()
    {
        var report = StaffPerformanceCalculator.Productivity(1, From, To,
            [Work(WorkKind.CleaningTask, WorkArea.Housekeeping, 99, 5, 30)], new Dictionary<int, StaffMember>());

        var collaborator = report.Collaborators.Should().ContainSingle().Subject;
        collaborator.UserId.Should().Be(99);
        collaborator.FullName.Should().BeNull();
    }

    [Fact]
    public void ServiceTimes_ShouldAverageTheResponseWorkAndTotalTimes()
    {
        var work = new[]
        {
            Work(WorkKind.CleaningTask, WorkArea.Housekeeping, 7, 10, 30),
            Work(WorkKind.CleaningTask, WorkArea.Housekeeping, 7, 20, 50)
        };

        var cleaning = StaffPerformanceCalculator.ServiceTimes(1, From, To, work).Cleaning;

        cleaning.Should().Be(new ServiceTimeIndicator(2, AverageResponseMinutes: 15, AverageWorkMinutes: 40,
            AverageTotalMinutes: 55, LongestTotalMinutes: 70));
    }

    [Fact]
    public void ServiceTimes_ShouldNotMeasurePreventiveMaintenance()
    {
        var work = new[]
        {
            Work(WorkKind.Incident, WorkArea.Maintenance, 20, 30, 60),
            Work(WorkKind.Incident, WorkArea.Maintenance, 20, 5000, 60, countsForServiceTimes: false)
        };

        var incidents = StaffPerformanceCalculator.ServiceTimes(1, From, To, work).Incidents;

        incidents.Count.Should().Be(1);
        incidents.AverageTotalMinutes.Should().Be(90);
    }

    [Fact]
    public void ServiceTimes_ShouldLeaveTheWorkTimeOut_WhenTheWorkWasNeverStarted()
    {
        var work = new[]
        {
            Work(WorkKind.GuestRequest, WorkArea.FrontDesk, 5, 0, null),
            Work(WorkKind.GuestRequest, WorkArea.FrontDesk, 5, 4, 6)
        };

        var requests = StaffPerformanceCalculator.ServiceTimes(1, From, To, work).GuestRequests;

        requests.Count.Should().Be(2);
        requests.AverageWorkMinutes.Should().Be(6);
        requests.AverageResponseMinutes.Should().Be(4);
        requests.AverageTotalMinutes.Should().Be(5);
    }

    [Fact]
    public void ServiceTimes_ShouldSplitTheGuestRequestsByArea()
    {
        var work = new[]
        {
            Work(WorkKind.GuestRequest, WorkArea.Housekeeping, 7, 5, 10),
            Work(WorkKind.GuestRequest, WorkArea.Maintenance, 20, 20, 40)
        };

        var report = StaffPerformanceCalculator.ServiceTimes(1, From, To, work);

        report.GuestRequests.AverageTotalMinutes.Should().Be(37.5);
        report.GuestRequestsByArea.Single(a => a.Area == WorkArea.Housekeeping).Indicator.AverageTotalMinutes.Should().Be(15);
        report.GuestRequestsByArea.Single(a => a.Area == WorkArea.Maintenance).Indicator.AverageTotalMinutes.Should().Be(60);
        report.GuestRequestsByArea.Single(a => a.Area == WorkArea.FrontDesk).Indicator.Count.Should().Be(0);
    }

    [Fact]
    public void ServiceTimes_WithoutWork_ShouldHaveNoAverages()
    {
        var report = StaffPerformanceCalculator.ServiceTimes(1, From, To, []);

        report.Cleaning.Should().Be(new ServiceTimeIndicator(0, null, null, null, null));
    }
}
