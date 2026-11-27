using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using RoomTrack.Housekeeping.Application.ACL;
using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Queries;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Housekeeping.Domain.Repositories;
using Xunit;

namespace RoomTrack.Tests.Housekeeping.Application;

/// <summary>When a room being cleaned will be ready for the guest waiting for it (US-13).</summary>
public class RoomCleaningEstimatesTests
{
    private const int HotelId = 1;
    private const int RoomId = 204;
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 20, 0, 0, TimeSpan.Zero);
    private static readonly CleaningTaskActor Front = new(5, "recepcion@roomtrack.pe", CleaningTaskRole.FrontDesk);
    private static readonly CleaningTaskActor Housekeeper = new(9, "rosa@roomtrack.pe", CleaningTaskRole.Housekeeper);

    private readonly FakeTasks _tasks = new();

    private Task<DateTimeOffset> Estimate() => new RoomCleaningEstimates(_tasks, new FixedClock(Now)).EstimateCleanAtAsync(RoomId, HotelId);

    [Fact]
    public async Task A_task_in_progress_ends_a_typical_cleaning_after_it_started()
    {
        _tasks.Durations = [TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(35)];
        _tasks.Open = StartedTask(startedAt: Now.AddMinutes(-20));

        // Median 35 minutes, started 20 minutes ago.
        (await Estimate()).Should().Be(Now.AddMinutes(15));
    }

    [Fact]
    public async Task A_task_nobody_started_takes_a_whole_typical_cleaning_from_now()
    {
        _tasks.Durations = [TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(400), TimeSpan.FromMinutes(25)];
        _tasks.Open = CleaningTask.CreateForRoomToClean(HotelId, RoomId, Now.AddMinutes(-5));

        // Median of 20, 25, 30 and 400: one forgotten task does not skew it.
        (await Estimate()).Should().Be(Now.AddMinutes(27.5));
    }

    [Fact]
    public async Task A_hotel_without_history_assumes_the_default_cleaning_time()
    {
        (await Estimate()).Should().Be(Now.AddMinutes(RoomCleaningEstimates.DefaultCleaningMinutes));
    }

    [Fact]
    public async Task A_late_cleaning_is_never_estimated_in_the_past()
    {
        _tasks.Durations = [TimeSpan.FromMinutes(30)];
        _tasks.Open = StartedTask(startedAt: Now.AddMinutes(-50));

        (await Estimate()).Should().Be(Now + RoomCleaningEstimates.MinimumWait);
    }

    private static CleaningTask StartedTask(DateTimeOffset startedAt)
    {
        var task = CleaningTask.CreateForRoomToClean(HotelId, RoomId, startedAt.AddMinutes(-10));
        task.AssignTo(Housekeeper.UserId, Front, startedAt.AddMinutes(-5));
        task.Start(Housekeeper, startedAt);
        return task;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeTasks : ICleaningTaskRepository
    {
        public IReadOnlyList<TimeSpan> Durations { get; set; } = [];
        public CleaningTask? Open { get; set; }

        public Task<IReadOnlyList<TimeSpan>> ListRecentCleaningDurationsAsync(int hotelId, int take) => Task.FromResult(Durations);
        public Task<CleaningTask?> FindOpenByRoomAsync(int roomId) => Task.FromResult(Open);

        public Task AddAsync(CleaningTask task) => throw new NotSupportedException();
        public Task<CleaningTask?> FindByIdAsync(int id) => throw new NotSupportedException();
        public Task<IReadOnlyList<CleaningTask>> ListAsync(GetCleaningTasksQuery query) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, int>> CountOpenByAssigneeAsync(int hotelId) => throw new NotSupportedException();
    }
}
