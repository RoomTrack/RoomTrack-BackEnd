using System;
using System.Linq;
using FluentAssertions;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Dashboard.Domain.Model;
using RoomTrack.Dashboard.Domain.Services;
using Xunit;

namespace RoomTrack.Tests.Dashboard;

/// <summary>Rules of the administrator's panel (US-18).</summary>
public class AdminDashboardCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private static HotelOperations Operations(
        RoomSnapshot[]? rooms = null, OpenCleaningTask[]? tasks = null, OpenIncident[]? incidents = null,
        OpenGuestRequest[]? requests = null) =>
        new(rooms ?? [], tasks ?? [], incidents ?? [], requests ?? []);

    [Fact]
    public void Rooms_ShouldBeCountedByStatus_WithTheOccupancy()
    {
        var operations = Operations(rooms:
        [
            new(1, "101", "Occupied"), new(2, "102", "Occupied"), new(3, "103", "Available"),
            new(4, "104", "Cleaning"), new(5, "105", "Maintenance"), new(6, "106", "Available")
        ]);

        var rooms = AdminDashboardCalculator.Build(1, operations, null, Hour, Now).Rooms;

        rooms.Should().Be(new RoomsSummary(6, 2, 2, 1, 1, 33.3));
    }

    [Fact]
    public void AHotelWithoutRooms_ShouldHaveNoOccupancy() =>
        AdminDashboardCalculator.Build(1, Operations(), null, Hour, Now).Rooms.OccupancyPercent.Should().Be(0);

    [Fact]
    public void CleaningTasks_ShouldCountTheUnassignedAndTheOverdueOnes()
    {
        var operations = Operations(tasks:
        [
            new(1, 1, "Normal", AssigneeUserId: null, Now.AddMinutes(-10)),
            new(2, 2, "High", AssigneeUserId: 30, Now.AddMinutes(-60)),
            new(3, 3, "Normal", AssigneeUserId: 30, Now.AddMinutes(-59))
        ]);

        AdminDashboardCalculator.Build(1, operations, null, Hour, Now).Cleaning.Should().Be(new CleaningSummary(3, 1, 1, 60));
    }

    [Fact]
    public void Attention_ShouldListEscalatedThenCriticalThenLateRequests()
    {
        var operations = Operations(
            rooms: [new(1, "101", "Occupied"), new(2, "102", "Occupied")],
            incidents:
            [
                new(10, 1, "Cerradura rota", "Critical", true, Now.AddHours(-1), null, false),
                new(11, 2, "Sin agua caliente", "Critical", false, Now.AddHours(-3), Now.AddHours(-2), false),
                new(12, 2, "Foco quemado", "Low", false, Now.AddHours(-5), null, true)
            ],
            requests:
            [
                new(20, 1, "Toallas", "Housekeeping", "Pending", Now.AddHours(-2), Now.AddMinutes(-30)),
                new(21, 2, "Almohada", "Housekeeping", "InProgress", Now.AddMinutes(-5), Now.AddMinutes(25))
            ]);

        var dashboard = AdminDashboardCalculator.Build(1, operations, null, Hour, Now);

        dashboard.Attention.Select(item => (item.Kind, item.ReferenceId, item.Reason, item.RoomNumber)).Should().Equal(
            ("Incident", 11, "Escalated", "102"),
            ("Incident", 10, "Critical", "101"),
            ("GuestRequest", 20, "Late", "101"));
        dashboard.Incidents.Should().Be(new IncidentsSummary(3, 2, 0, 1, 1, 1));
        dashboard.GuestRequests.Should().Be(new GuestRequestsSummary(2, 1, 1));
    }

    [Fact]
    public void Attention_ShouldListAtMost10Items()
    {
        var incidents = Enumerable.Range(1, 15)
            .Select(i => new OpenIncident(i, 1, $"Incidencia {i}", "Critical", false, Now.AddMinutes(-i), null, false))
            .ToArray();

        AdminDashboardCalculator.Build(1, Operations(incidents: incidents), null, Hour, Now).Attention
            .Should().HaveCount(AdminDashboardCalculator.MaxAttentionItems);
    }

    [Fact]
    public void TheFrontDesk_ShouldBePassedThrough()
    {
        var frontDesk = new FrontDeskSummary(3, 5, 2, 4, 4.25, []);

        AdminDashboardCalculator.Build(1, Operations(), frontDesk, Hour, Now).FrontDesk.Should().BeSameAs(frontDesk);
    }
}
