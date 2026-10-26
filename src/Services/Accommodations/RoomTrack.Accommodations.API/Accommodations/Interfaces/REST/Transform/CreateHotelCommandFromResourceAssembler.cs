using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Domain.Model.ValueObjects;
using RoomTrack.Accommodations.Interfaces.REST.Resources;
using RoomTrack.IAM.Interfaces.ACL;

namespace RoomTrack.Accommodations.Interfaces.REST.Transform;

public static class CreateHotelCommandFromResourceAssembler
{
    public static CreateHotelCommand ToCommandFromResource(CreateHotelResource resource, HotelRegistrant registrant,
        SessionContext registrantSession)
    {
        return new CreateHotelCommand(
            registrant,
            resource.HostId,
            resource.Name,
            resource.Address, 
            resource.City,    
            resource.Country,
            resource.ImageUrl,
            resource.Description,
            resource.Type,
            resource.Amenities,
            registrantSession
        );
    }
}