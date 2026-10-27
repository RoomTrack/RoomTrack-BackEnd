using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Interfaces.REST.Resources;

namespace RoomTrack.Accommodations.Interfaces.REST.Transform;

public static class CreateRoomTypeCommandFromResourceAssembler
{
    public static CreateRoomTypeCommand ToCommandFromResource(CreateRoomTypeResource resource)
    {
        return new CreateRoomTypeCommand(resource.Name, resource.Description);
    }
}

