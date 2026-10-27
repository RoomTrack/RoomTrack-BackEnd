using System;
using System.Linq;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Events;
using RoomTrack.GuestRequests.Domain.Model.Exceptions;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.Shared.Domain.Model.Exceptions;
using FluentAssertions;
using Xunit;

namespace RoomTrack.Tests.GuestRequests;

/// <summary>Rules of a guest request (US-11, US-19).</summary>
public class GuestRequestTests
{
    private static readonly GuestRequestActor Guest = new(50, "huesped@example.com", UserRoles.Guest);
    private static readonly GuestRequestActor Reception = new(10, "recepcion@roomtrack.pe", UserRoles.Reception);
    private static readonly GuestRequestActor Housekeeper = new(30, "ana@roomtrack.pe", UserRoles.Housekeeping);
    private static readonly GuestRequestActor Maintenance = new(20, "mantenimiento@roomtrack.pe", UserRoles.Maintenance);
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 15, 0, 0, TimeSpan.Zero);

    private static GuestRequest SentByGuest(ServiceArea? area = null) =>
        GuestRequest.SendByGuest(1, 101, 7, "Toallas extra", null, area, Guest, Now.AddMinutes(15), Now);

    [Fact]
    public void AGuestRequest_ShouldArriveAtTheFrontDesk_WithItsEstimatedAttention()
    {
        var request = SentByGuest();

        request.Area.Should().Be(ServiceArea.Reception);
        request.Status.Should().Be(GuestRequestStatus.Pending);
        request.GuestUserId.Should().Be(Guest.UserId);
        request.EstimatedAttentionBy.Should().Be(Now.AddMinutes(15));
        request.CreatedByGuest.Should().BeTrue();
    }

    [Fact]
    public void TheFrontDesk_ShouldRouteItToTheAreaThatAttendsIt()
    {
        var request = SentByGuest();

        request.RouteTo(ServiceArea.Housekeeping, Reception, Now.AddMinutes(30), Now);

        request.Area.Should().Be(ServiceArea.Housekeeping);
        request.EstimatedAttentionBy.Should().Be(Now.AddMinutes(30));
        request.DomainEvents.OfType<GuestRequestRoutedEvent>().Should().ContainSingle()
            .Which.ToArea.Should().Be(ServiceArea.Housekeeping);
    }

    [Fact]
    public void OnlyTheFrontDesk_ShouldRoute()
    {
        var request = SentByGuest();

        var act = () => request.RouteTo(ServiceArea.Maintenance, Housekeeper, Now, Now);

        act.Should().Throw<GuestRequestNotAllowedException>();
    }

    [Fact]
    public void TheStaffOfTheArea_ShouldTakeAndResolveIt_AndTheGuestFollowsEachStatus()
    {
        var request = SentByGuest(ServiceArea.Housekeeping);

        request.AssignTo(Housekeeper.UserId, Housekeeper, Now);
        request.Start(Housekeeper, Now.AddMinutes(5));
        request.Resolve("Se dejaron 2 toallas", Housekeeper, Now.AddMinutes(10));

        request.Status.Should().Be(GuestRequestStatus.Resolved);
        request.AssigneeUserId.Should().Be(Housekeeper.UserId);
        request.ClosingNote.Should().Be("Se dejaron 2 toallas");
        request.DomainEvents.OfType<GuestRequestStatusChangedEvent>().Select(e => e.Status)
            .Should().Equal(GuestRequestStatus.InProgress, GuestRequestStatus.Resolved);
        request.History.Select(h => h.Action).Should().Equal(GuestRequestHistoryAction.Created,
            GuestRequestHistoryAction.Assigned, GuestRequestHistoryAction.Started, GuestRequestHistoryAction.Resolved);
    }

    [Fact]
    public void AnotherArea_ShouldNotAttendIt()
    {
        var request = SentByGuest(ServiceArea.Housekeeping);

        var act = () => request.Start(Maintenance, Now);

        act.Should().Throw<GuestRequestNotAllowedException>();
    }

    [Fact]
    public void TheGuest_ShouldWithdrawItOnlyWhileNobodyStartedIt()
    {
        var pending = SentByGuest();
        var started = SentByGuest();
        started.Start(Reception, Now);

        pending.Cancel("Ya no lo necesito", Guest, Now);
        var act = () => started.Cancel(null, Guest, Now);

        pending.Status.Should().Be(GuestRequestStatus.Cancelled);
        act.Should().Throw<GuestRequestNotAllowedException>();
    }

    [Fact]
    public void AResolvedRequest_ShouldNotChangeAnymore()
    {
        var request = SentByGuest();
        request.Resolve(null, Reception, Now);

        var act = () => request.RouteTo(ServiceArea.Maintenance, Reception, Now, Now);

        act.Should().Throw<GuestRequestClosedException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void ATitleTooShort_ShouldBeRefused(string title)
    {
        var act = () => GuestRequest.SendByGuest(1, 101, 7, title, null, null, Guest, Now, Now);

        act.Should().Throw<InvalidFieldException>().Which.Violation.Code.Should().Be(GuestRequestErrorCodes.TitleLength);
    }
}
