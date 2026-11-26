using RoomTrack.ShiftHandovers.Domain.Model.Aggregates;
using RoomTrack.ShiftHandovers.Domain.Model.Queries;
using RoomTrack.ShiftHandovers.Domain.Repositories;
using RoomTrack.ShiftHandovers.Domain.Services;

namespace RoomTrack.ShiftHandovers.Application.Internal.QueryServices;

public class ShiftHandoverQueryService(IShiftHandoverRepository handoverRepository) : IShiftHandoverQueryService
{
    public Task<ShiftHandover?> Handle(GetShiftHandoverByIdQuery query) => handoverRepository.FindByIdAsync(query.ShiftHandoverId);

    public Task<IReadOnlyList<ShiftHandover>> Handle(GetShiftHandoversQuery query) =>
        handoverRepository.ListAsync(query.HotelId, query.Area, Math.Clamp(query.Limit, 1, 100));

    public async Task<ShiftHandover?> Handle(GetLatestShiftHandoverQuery query) =>
        (await handoverRepository.ListAsync(query.HotelId, query.Area, 1)).FirstOrDefault();
}
