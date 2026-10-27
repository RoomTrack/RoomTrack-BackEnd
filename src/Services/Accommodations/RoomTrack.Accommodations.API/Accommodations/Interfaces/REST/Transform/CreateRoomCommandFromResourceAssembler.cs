using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Interfaces.REST.Resources;

namespace RoomTrack.Accommodations.Interfaces.REST.Transform;

public static class CreateRoomCommandFromResourceAssembler
{
    public static CreateRoomCommand ToCommandFromResource(CreateRoomResource resource) =>
        new(resource.HotelId, resource.RoomTypeId, resource.Price, resource.Description!.Trim(),
            resource.Amenities ?? [], resource.Number!);
}
