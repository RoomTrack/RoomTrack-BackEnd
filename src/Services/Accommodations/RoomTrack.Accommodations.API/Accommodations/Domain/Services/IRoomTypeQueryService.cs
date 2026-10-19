using BackendAwRoomTrack.API.Accommodations.Domain.Model.Entities;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Queries;

namespace BackendAwRoomTrack.API.Accommodations.Domain.Services;

public interface IRoomTypeQueryService
{

    Task<RoomType?> Handle(GetRoomTypeByIdQuery query);
    
    Task<IEnumerable<RoomType>> Handle(GetAllRoomTypesQuery query);
}

