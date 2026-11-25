using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Queries;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Housekeeping.Domain.Repositories;
using RoomTrack.Housekeeping.Domain.Services;

namespace RoomTrack.Housekeeping.Application.Internal.QueryServices;

public class CleaningTaskQueryService(
    ICleaningTaskRepository cleaningTaskRepository,
    IHotelHousekeepingSettingsRepository settingsRepository,
    ICleaningChecklistTemplateRepository checklistTemplateRepository) : ICleaningTaskQueryService
{
    public Task<CleaningTask?> Handle(GetCleaningTaskByIdQuery query) => cleaningTaskRepository.FindByIdAsync(query.CleaningTaskId);

    public Task<IReadOnlyList<CleaningTask>> Handle(GetCleaningTasksQuery query) =>
        cleaningTaskRepository.ListAsync(query with { Limit = Math.Clamp(query.Limit, 1, 500) });

    public async Task<HotelHousekeepingSettings> Handle(GetHousekeepingSettingsQuery query) =>
        await settingsRepository.FindByHotelIdAsync(query.HotelId) ?? new HotelHousekeepingSettings(query.HotelId);

    public Task<IReadOnlyList<CleaningChecklistTemplate>> Handle(GetCleaningChecklistTemplatesQuery query) =>
        checklistTemplateRepository.ListByHotelAsync(query.HotelId);

    public Task<IReadOnlyList<ChecklistItemDefinition>> Handle(GetCleaningChecklistForRoomQuery query) =>
        checklistTemplateRepository.ResolveForRoomTypeAsync(query.HotelId, query.RoomTypeId);
}
