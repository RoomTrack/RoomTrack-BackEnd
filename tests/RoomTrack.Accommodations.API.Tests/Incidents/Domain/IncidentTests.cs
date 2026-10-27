using System;
using System.Linq;
using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Events;
using RoomTrack.Incidents.Domain.Model.Exceptions;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;
using FluentAssertions;
using Xunit;

namespace RoomTrack.Tests.Incidents.Domain;

public class IncidentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 14, 0, 0, TimeSpan.Zero);
    private static readonly IncidentActor Reception = new(UserId: 10, "recepcion@roomtrack.pe", IsAdministrator: false);
    private static readonly IncidentActor Maintenance = new(UserId: 20, "mantenimiento@roomtrack.pe", IsAdministrator: false);
    private static readonly IncidentActor OtherMaintenance = new(UserId: 21, "mantenimiento2@roomtrack.pe", IsAdministrator: false);
    private static readonly IncidentActor Admin = new(UserId: 1, "admin@roomtrack.pe", IsAdministrator: true);

    private static Incident Report(IncidentPriority priority = IncidentPriority.High, bool blocksRoom = false) =>
        Incident.Report(hotelId: 1, roomId: 12, "  Leaking shower  ", "Water drips.", priority, blocksRoom, Reception, Now);

    [Fact]
    public void Report_ShouldStartPendingUnassigned_WithTrimmedTitle_AndReportedHistory()
    {
        var incident = Report();

        incident.Status.Should().Be(IncidentStatus.Pending);
        incident.Title.Should().Be("Leaking shower");
        incident.AssigneeUserId.Should().BeNull();
        incident.ReportedByUserId.Should().Be(Reception.UserId);
        incident.ReportedAt.Should().Be(Now);
        incident.History.Should().ContainSingle().Which.Action.Should().Be(IncidentHistoryAction.Reported);
    }

    [Fact]
    public void Report_ShouldNotRaiseTheReportedEvent_UntilTheIncidentHasItsId()
    {
        var incident = Report(IncidentPriority.Critical, blocksRoom: true);

        incident.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Report_WithTooShortTitle_ShouldFail(string title)
    {
        var act = () => Incident.Report(1, 12, title, null, IncidentPriority.Low, false, Reception, Now);

        act.Should().Throw<InvalidFieldException>().Which.Violation.Code.Should().Be(IncidentErrorCodes.TitleLength);
    }

    [Fact]
    public void AssignTo_ByAdministrator_ShouldAssignAnyone_AndRaiseEvent()
    {
        var incident = Report();

        incident.AssignTo(Maintenance.UserId, Admin, Now);

        incident.AssigneeUserId.Should().Be(Maintenance.UserId);
        incident.AssignedAt.Should().Be(Now);
        incident.DomainEvents.OfType<IncidentAssignedEvent>().Should().ContainSingle()
            .Which.AssigneeUserId.Should().Be(Maintenance.UserId);
    }

    [Fact]
    public void AssignTo_MaintenanceTakingItForThemselves_ShouldSucceed()
    {
        var incident = Report();

        incident.AssignTo(Maintenance.UserId, Maintenance, Now);

        incident.AssigneeUserId.Should().Be(Maintenance.UserId);
    }

    [Fact]
    public void AssignTo_MaintenanceAssigningSomeoneElse_ShouldBeForbidden()
    {
        var incident = Report();

        var act = () => incident.AssignTo(OtherMaintenance.UserId, Maintenance, Now);

        act.Should().Throw<OperationNotAllowedException>().Which.Code.Should().Be(IncidentErrorCodes.AssignNotAllowed);
    }

    [Fact]
    public void AssignTo_MaintenanceTakingAnIncidentOfAnotherAssignee_ShouldBeForbidden()
    {
        var incident = Report();
        incident.AssignTo(OtherMaintenance.UserId, Admin, Now);

        var act = () => incident.AssignTo(Maintenance.UserId, Maintenance, Now);

        act.Should().Throw<OperationNotAllowedException>();
    }

    [Fact]
    public void Start_WithoutAssignee_ShouldFail()
    {
        var incident = Report();

        var act = () => incident.Start(Admin, Now);

        act.Should().Throw<IncidentNotAssignedException>();
    }

    [Fact]
    public void Start_ByTheAssignee_ShouldMoveToInProgress()
    {
        var incident = Report();
        incident.AssignTo(Maintenance.UserId, Admin, Now);

        incident.Start(Maintenance, Now.AddMinutes(5));

        incident.Status.Should().Be(IncidentStatus.InProgress);
        incident.StartedAt.Should().Be(Now.AddMinutes(5));
        incident.DomainEvents.OfType<IncidentStartedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Start_BySomeoneOtherThanTheAssignee_ShouldBeForbidden()
    {
        var incident = Report();
        incident.AssignTo(Maintenance.UserId, Admin, Now);

        var act = () => incident.Start(OtherMaintenance, Now);

        act.Should().Throw<NotIncidentAssigneeException>();
    }

    [Fact]
    public void Resolve_WhilePending_ShouldFail()
    {
        var incident = Report();
        incident.AssignTo(Maintenance.UserId, Admin, Now);

        var act = () => incident.Resolve("Fixed.", Maintenance, Now);

        act.Should().Throw<InvalidIncidentStatusTransitionException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Resolve_WithoutNotes_ShouldFail(string? notes)
    {
        var incident = Report();
        incident.AssignTo(Maintenance.UserId, Admin, Now);
        incident.Start(Maintenance, Now);

        var act = () => incident.Resolve(notes, Maintenance, Now);

        act.Should().Throw<InvalidFieldException>().Which.Violation.Code.Should().Be(IncidentErrorCodes.ResolutionNotesRequired);
    }

    [Fact]
    public void Resolve_ByTheAssignee_ShouldCloseIt_RecordTheNotes_AndBlockFurtherChanges()
    {
        var incident = Report(blocksRoom: true);
        incident.AssignTo(Maintenance.UserId, Admin, Now);
        incident.Start(Maintenance, Now);

        incident.Resolve("  Replaced the cartridge.  ", Maintenance, Now.AddHours(1));

        incident.Status.Should().Be(IncidentStatus.Resolved);
        incident.IsOpen.Should().BeFalse();
        incident.ResolutionNotes.Should().Be("Replaced the cartridge.");
        incident.ResolvedByUserId.Should().Be(Maintenance.UserId);
        incident.DomainEvents.OfType<IncidentResolvedEvent>().Should().ContainSingle().Which.BlocksRoom.Should().BeTrue();
        incident.History.Select(entry => entry.Action).Should().Equal(
            IncidentHistoryAction.Reported, IncidentHistoryAction.Assigned, IncidentHistoryAction.Started, IncidentHistoryAction.Resolved);

        var act = () => incident.ChangePriority(IncidentPriority.Low, Admin, Now);
        act.Should().Throw<IncidentAlreadyResolvedException>();
    }

    [Fact]
    public void ChangePriority_ShouldRecordTheChange()
    {
        var incident = Report(IncidentPriority.Low);

        incident.ChangePriority(IncidentPriority.Critical, Admin, Now);

        incident.Priority.Should().Be(IncidentPriority.Critical);
        incident.History.Last().Action.Should().Be(IncidentHistoryAction.PriorityChanged);
    }

    [Fact]
    public void Escalate_ACriticalIncidentNotStartedInTime_ShouldHappenOnce()
    {
        var incident = Report(IncidentPriority.Critical);
        var threshold = TimeSpan.FromMinutes(60);

        incident.Escalate(Now.AddMinutes(30), threshold).Should().BeFalse();
        incident.Escalate(Now.AddMinutes(61), threshold).Should().BeTrue();
        incident.Escalate(Now.AddMinutes(120), threshold).Should().BeFalse();

        incident.EscalatedAt.Should().Be(Now.AddMinutes(61));
        incident.DomainEvents.OfType<IncidentEscalatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Escalate_ANonCriticalOrStartedIncident_ShouldNotHappen()
    {
        var high = Report(IncidentPriority.High);
        var started = Report(IncidentPriority.Critical);
        started.AssignTo(Maintenance.UserId, Admin, Now);
        started.Start(Maintenance, Now);

        high.Escalate(Now.AddDays(1), TimeSpan.FromMinutes(60)).Should().BeFalse();
        started.Escalate(Now.AddDays(1), TimeSpan.FromMinutes(60)).Should().BeFalse();
    }
}
