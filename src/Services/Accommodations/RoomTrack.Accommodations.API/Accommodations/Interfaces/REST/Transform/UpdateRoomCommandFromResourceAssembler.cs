using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Interfaces.REST.Resources;

namespace RoomTrack.Accommodations.Interfaces.REST.Transform;

public static class UpdateRoomCommandFromResourceAssembler
{
    public static UpdateRoomCommand ToCommandFromResource(int roomId, UpdateRoomResource resource) =>
        new(roomId, resource.RoomTypeId, resource.Price, resource.Description!.Trim(), resource.Amenities ?? [],
            resource.Number);
}
