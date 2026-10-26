using RoomTrack.Accommodations.Domain.Model.Entities;
using RoomTrack.Accommodations.Domain.Model.Queries;

namespace RoomTrack.Accommodations.Domain.Services;

public interface IRoomTypeQueryService
{

    Task<RoomType?> Handle(GetRoomTypeByIdQuery query);
    
    Task<IEnumerable<RoomType>> Handle(GetAllRoomTypesQuery query);
}

