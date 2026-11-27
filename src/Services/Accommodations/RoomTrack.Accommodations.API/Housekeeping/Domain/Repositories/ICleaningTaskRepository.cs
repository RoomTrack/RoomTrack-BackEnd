using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
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

    /// <summary>
    ///     How long the latest <paramref name="take"/> completed cleanings of the hotel took (start to completion),
    ///     newest first; cleanings left open for more than a working shift are left out.
    /// </summary>
    Task<IReadOnlyList<TimeSpan>> ListRecentCleaningDurationsAsync(int hotelId, int take);
}

public interface IHotelHousekeepingSettingsRepository
{
    Task<HotelHousekeepingSettings?> FindByHotelIdAsync(int hotelId);

    Task AddAsync(HotelHousekeepingSettings settings);
}

public interface ICleaningChecklistTemplateRepository
{
    /// <summary>The protocol of the hotel for the room type (null: the hotel's default), with its items, or null.</summary>
    Task<CleaningChecklistTemplate?> FindAsync(int hotelId, int? roomTypeId);

    /// <summary>Every protocol of the hotel, with their items.</summary>
    Task<IReadOnlyList<CleaningChecklistTemplate>> ListByHotelAsync(int hotelId);

    Task AddAsync(CleaningChecklistTemplate template);
}

public static class CleaningChecklistTemplateRepositoryExtensions
{
    /// <summary>
    ///     The steps a new cleaning task of a room of <paramref name="roomTypeId"/> gets (US-07): the hotel's protocol for
    ///     that type, else the hotel's default protocol, else none (a hotel that configured no protocol keeps completing
    ///     its tasks without a checklist).
    /// </summary>
    public static async Task<IReadOnlyList<ChecklistItemDefinition>> ResolveForRoomTypeAsync(
        this ICleaningChecklistTemplateRepository repository, int hotelId, int roomTypeId) =>
        (await repository.FindAsync(hotelId, roomTypeId) ?? await repository.FindAsync(hotelId, null))?.Definitions
        ?? [];
}
