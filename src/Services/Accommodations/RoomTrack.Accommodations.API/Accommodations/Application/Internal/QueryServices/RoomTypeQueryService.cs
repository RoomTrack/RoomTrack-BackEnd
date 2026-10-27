using RoomTrack.Accommodations.Domain.Model.Entities;
using RoomTrack.Accommodations.Domain.Model.Queries;
using RoomTrack.Accommodations.Domain.Repositories;
using RoomTrack.Accommodations.Domain.Services;

namespace RoomTrack.Accommodations.Application.Internal.QueryServices;

// Service responsible for handling room type queries
public class RoomTypeQueryService(IRoomTypeRepository roomTypeRepository)
    : IRoomTypeQueryService
{
    // Retrieve a room type by its identifier
    public async Task<RoomType?> Handle(GetRoomTypeByIdQuery query)
    {
        return await roomTypeRepository.FindByIdAsync(query.RoomTypeId);
    }

    // Retrieve all room types
    public async Task<IEnumerable<RoomType>> Handle(GetAllRoomTypesQuery query)
    {
        return await roomTypeRepository.ListAsync();
    }
}

