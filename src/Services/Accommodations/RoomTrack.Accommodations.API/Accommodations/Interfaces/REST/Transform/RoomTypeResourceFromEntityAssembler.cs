using BackendAwRoomTrack.API.Accommodations.Domain.Model.Entities;
using BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Resources;

namespace BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Transform;


public static class RoomTypeResourceFromEntityAssembler
{

    public static RoomTypeResource ToResourceFromEntity(RoomType entity)
    {
        return new RoomTypeResource(entity.Id, entity.Name, entity.Description);
    }
}

