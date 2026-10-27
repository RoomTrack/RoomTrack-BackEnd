using RoomTrack.Accommodations.Domain.Model.Entities;
using RoomTrack.Accommodations.Interfaces.REST.Resources;

namespace RoomTrack.Accommodations.Interfaces.REST.Transform;


public static class RoomTypeResourceFromEntityAssembler
{

    public static RoomTypeResource ToResourceFromEntity(RoomType entity)
    {
        return new RoomTypeResource(entity.Id, entity.Name, entity.Description);
    }
}

