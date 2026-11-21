using System;
using System.Threading.Tasks;
using FluentAssertions;
using Reqnroll;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Tests.Features.Support;

/// <summary>
///     Outcome of the "intenta..." steps, shared by every binding class of a scenario (Reqnroll injects the same
///     instance): the operation's error, if the system refused it.
/// </summary>
public class ScenarioOutcome
{
    public Exception? Error { get; private set; }

    public void Capture(Action attempt)
    {
        try { attempt(); }
        catch (DomainException e) { Error = e; }
    }

    public async Task CaptureAsync(Func<Task> attempt)
    {
        try { await attempt(); }
        catch (DomainException e) { Error = e; }
    }
}

[Binding]
public class CommonSteps(ScenarioOutcome outcome)
{
    [Then("el sistema rechaza la operación")]
    public void ThenTheSystemRefusesTheOperation() =>
        outcome.Error.Should().NotBeNull("the system should have refused the operation");
}
