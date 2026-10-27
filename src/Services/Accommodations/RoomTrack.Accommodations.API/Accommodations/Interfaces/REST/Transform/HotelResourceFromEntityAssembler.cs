using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Interfaces.REST.Resources;

namespace RoomTrack.Accommodations.Interfaces.REST.Transform;

public static class HotelResourceFromEntityAssembler
{
    public static HotelResource ToResourceFromEntity(Hotel entity)
    {
        var locationDisplay = $"{entity.Address}, {entity.City}, {entity.Country}";
        
        var lowestPrice = entity.CalculateLowestPrice();

        return new HotelResource(
            entity.Id,
            entity.HostId,
            entity.Name,
            locationDisplay, 
            entity.ImageUrl,
            entity.Description,
            lowestPrice,
            entity.Type,
            entity.Amenities,
            entity.AcceptsBookings
        );
    }
}