using System;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BackendAwRoomTrack.API.Tests.Accommodations.Domain;

public class RoomIncidentBlockTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 14, 0, 0, TimeSpan.Zero);

    private static Room NewRoom() => new(new CreateRoomCommand(1, 1, 120m, "Room 101", [], "101"));

    [Fact]
    public void HoldForIncident_ShouldTakeTheRoomToMaintenance_AndWithholdAvailable()
    {
        var room = NewRoom();

        var change = room.HoldForIncident(10, "recepcion@roomtrack.pe", Now);

        room.Status.Should().Be(RoomStatus.Maintenance);
        room.IsBlockedByIncident.Should().BeTrue();
        change!.Origin.Should().Be(RoomStatusChangeOrigin.Incident);
        room.AllowedNextStatuses.Should().Equal(RoomStatus.Cleaning);
    }

    [Fact]
    public void ABlockedRoom_ShouldNotBecomeAvailable_EvenAfterCleaning()
    {
        var room = NewRoom();
        room.HoldForIncident(10, null, Now);
        room.ChangeStatus(RoomStatus.Cleaning, RoomStatusChangeOrigin.Staff, 30, null, Now);

        var act = () => room.ChangeStatus(RoomStatus.Available, RoomStatusChangeOrigin.Staff, 30, null, Now);

        act.Should().Throw<RoomBlockedByIncidentException>();
    }

    [Fact]
    public void ReleaseFromIncident_ShouldSendTheRoomToCleaning_AndAllowAvailableAgain()
    {
        var room = NewRoom();
        room.HoldForIncident(10, null, Now);

        room.ReleaseFromIncident(20, null, Now);

        room.Status.Should().Be(RoomStatus.Cleaning);
        room.IsBlockedByIncident.Should().BeFalse();
        room.ChangeStatus(RoomStatus.Available, RoomStatusChangeOrigin.Staff, 30, null, Now);
        room.Status.Should().Be(RoomStatus.Available);
    }
}
