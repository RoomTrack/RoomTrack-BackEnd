using BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;
using BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Resources;

namespace BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Transform;

public static class CreateRoomTypeCommandFromResourceAssembler
{
    public static CreateRoomTypeCommand ToCommandFromResource(CreateRoomTypeResource resource)
    {
        return new CreateRoomTypeCommand(resource.Name, resource.Description);
    }
}

