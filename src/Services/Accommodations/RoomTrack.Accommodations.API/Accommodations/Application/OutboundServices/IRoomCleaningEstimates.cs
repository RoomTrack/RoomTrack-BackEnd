namespace RoomTrack.Accommodations.Application.OutboundServices;

/// <summary>
///     Port to the Housekeeping context: when a room being cleaned is expected to be clean (US-13, a guest waiting for
///     their room). Implemented by Housekeeping, which knows the room's cleaning task and how long cleanings take.
/// </summary>
public interface IRoomCleaningEstimates
{
    /// <summary>When the room is expected to be clean (UTC), never earlier than a few minutes from now.</summary>
    Task<DateTimeOffset> EstimateCleanAtAsync(int roomId, int hotelId);
}

/// <summary>
///     The estimate when the Housekeeping context is not part of the service: a typical cleaning from now. The
///     Housekeeping registration replaces it.
/// </summary>
public sealed class TypicalRoomCleaningEstimates(TimeProvider timeProvider) : IRoomCleaningEstimates
{
    public static readonly TimeSpan TypicalCleaning = TimeSpan.FromMinutes(45);

    public Task<DateTimeOffset> EstimateCleanAtAsync(int roomId, int hotelId) =>
        Task.FromResult(timeProvider.GetUtcNow() + TypicalCleaning);
}
