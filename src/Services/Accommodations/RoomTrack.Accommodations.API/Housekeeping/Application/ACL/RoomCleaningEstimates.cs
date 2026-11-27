using RoomTrack.Accommodations.Application.OutboundServices;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Housekeeping.Domain.Repositories;

namespace RoomTrack.Housekeeping.Application.ACL;

/// <summary>
///     Estimates when a room being cleaned will be clean (US-13): from the start of its cleaning task (or from now, while
///     nobody started it) plus how long the hotel's latest cleanings took (their median, so one forgotten task does not
///     skew it), or <see cref="DefaultCleaningMinutes"/> for a hotel without history.
/// </summary>
public class RoomCleaningEstimates(ICleaningTaskRepository cleaningTaskRepository, TimeProvider timeProvider) : IRoomCleaningEstimates
{
    /// <summary>How long a cleaning takes when the hotel has no completed cleanings yet.</summary>
    public const int DefaultCleaningMinutes = 45;

    /// <summary>Completed cleanings the estimate looks at.</summary>
    public const int Sample = 20;

    /// <summary>The estimate is never closer than this: the room still has to be checked and handed over.</summary>
    public static readonly TimeSpan MinimumWait = TimeSpan.FromMinutes(5);

    public async Task<DateTimeOffset> EstimateCleanAtAsync(int roomId, int hotelId)
    {
        var now = timeProvider.GetUtcNow();
        var durations = await cleaningTaskRepository.ListRecentCleaningDurationsAsync(hotelId, Sample);
        var typical = Median(durations) ?? TimeSpan.FromMinutes(DefaultCleaningMinutes);

        var task = await cleaningTaskRepository.FindOpenByRoomAsync(roomId);
        var from = task is { Status: CleaningTaskStatus.InProgress, StartedAt: { } startedAt } ? startedAt : now;
        var at = from + typical;
        return at < now + MinimumWait ? now + MinimumWait : at;
    }

    private static TimeSpan? Median(IReadOnlyList<TimeSpan> durations)
    {
        if (durations.Count == 0) return null;
        var sorted = durations.OrderBy(d => d).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
