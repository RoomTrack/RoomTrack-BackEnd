using BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Resources;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;

namespace BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Transform;

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