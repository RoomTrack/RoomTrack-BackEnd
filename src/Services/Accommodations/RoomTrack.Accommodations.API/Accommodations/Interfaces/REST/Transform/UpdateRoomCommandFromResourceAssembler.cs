using BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;
using BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Resources;

namespace BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Transform;

public static class UpdateRoomCommandFromResourceAssembler
{
    public static UpdateRoomCommand ToCommandFromResource(int roomId, UpdateRoomResource resource) =>
        new(roomId, resource.RoomTypeId, resource.Price, resource.Description!.Trim(), resource.Amenities ?? [],
            resource.Number);
}
