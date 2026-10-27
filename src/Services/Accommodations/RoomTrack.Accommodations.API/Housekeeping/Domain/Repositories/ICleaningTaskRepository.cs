using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Queries;

namespace RoomTrack.Housekeeping.Domain.Repositories;

public interface ICleaningTaskRepository
{
    Task AddAsync(CleaningTask task);

    /// <summary>The task with its history, or null.</summary>
    Task<CleaningTask?> FindByIdAsync(int id);

    /// <summary>The tasks matching the query, most urgent first (without their history).</summary>
    Task<IReadOnlyList<CleaningTask>> ListAsync(GetCleaningTasksQuery query);

    /// <summary>The open (Pending or InProgress) task of the room, with its history, or null.</summary>
    Task<CleaningTask?> FindOpenByRoomAsync(int roomId);

    /// <summary>How many open tasks each staff member of the hotel has assigned (only those with at least one).</summary>
    Task<IReadOnlyDictionary<int, int>> CountOpenByAssigneeAsync(int hotelId);
}

public interface IHotelHousekeepingSettingsRepository
{
    Task<HotelHousekeepingSettings?> FindByHotelIdAsync(int hotelId);

    Task AddAsync(HotelHousekeepingSettings settings);
}
