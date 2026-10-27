using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Interfaces.REST.Resources;

namespace RoomTrack.Accommodations.Interfaces.REST.Transform;

public static class RoomResourceFromEntityAssembler
{
    public static RoomResource ToResourceFromEntity(Room entity)
    {
        return new RoomResource(
            entity.Id,
            entity.HotelId,
            entity.RoomTypeId,
            entity.RoomType?.Name ?? "Unknown", // Safe navigation
            entity.Price,
            entity.Description,
            entity.Amenities,
            entity.Status.ToString(),
            entity.Number
        );
    }
}