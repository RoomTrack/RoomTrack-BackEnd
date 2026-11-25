using System;
using FluentAssertions;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.ShiftHandovers.Domain.Model.Aggregates;
using RoomTrack.ShiftHandovers.Domain.Model.Exceptions;
using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;
using Xunit;

namespace RoomTrack.Tests.ShiftHandovers;

/// <summary>Rules of the shift handover (US-32).</summary>
public class ShiftHandoverTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 23, 0, 0, TimeSpan.Zero);
    private static readonly ShiftHandoverActor Ana = new(30, "ana@roomtrack.pe", ShiftArea.Housekeeping);
    private static readonly ShiftHandoverActor Rosa = new(31, "rosa@roomtrack.pe", ShiftArea.Housekeeping);

    [Fact]
    public void AHandoverWithoutNotesNorOpenItems_ShouldBeRefused()
    {
        var create = () => ShiftHandover.Create(1, ShiftArea.Housekeeping, Ana, "   ", [], Now);

        create.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(ShiftHandoverErrorCodes.Empty);
    }

    [Fact]
    public void TheAuthor_ShouldNotConfirmTheirOwnHandover()
    {
        var handover = ShiftHandover.Create(1, ShiftArea.Housekeeping, Ana, "Todo en orden", [], Now);

        var confirm = () => handover.Acknowledge(Ana, Now);

        confirm.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(ShiftHandoverErrorCodes.OwnHandover);
    }

    [Fact]
    public void ConfirmingTwice_ShouldKeepTheFirstConfirmation()
    {
        var handover = ShiftHandover.Create(1, ShiftArea.Housekeeping, Ana, "Todo en orden", [], Now);

        handover.Acknowledge(Rosa, Now.AddMinutes(5));
        handover.Acknowledge(Rosa, Now.AddMinutes(30));

        handover.Acknowledgements.Should().ContainSingle().Which.AcknowledgedAt.Should().Be(Now.AddMinutes(5));
        handover.IsAcknowledgedBy(Rosa.UserId).Should().BeTrue();
    }
}
