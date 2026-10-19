using BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;
using BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Resources;

namespace BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Transform;

public static class CreateRoomCommandFromResourceAssembler
{
    public static CreateRoomCommand ToCommandFromResource(CreateRoomResource resource) =>
        new(resource.HotelId, resource.RoomTypeId, resource.Price, resource.Description!.Trim(),
            resource.Amenities ?? [], resource.Number!);
}
