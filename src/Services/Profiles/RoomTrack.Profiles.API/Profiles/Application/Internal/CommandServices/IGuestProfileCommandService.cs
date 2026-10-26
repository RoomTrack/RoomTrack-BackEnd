using RoomTrack.Profiles.Application.Internal.Commands;
using RoomTrack.Profiles.Domain.Model.Aggregates;

namespace RoomTrack.Profiles.Application.Internal.CommandServices;

public interface IGuestProfileCommandService
{
    Task<GuestProfile?> Handle(CreateGuestProfileCommand command);
    Task<GuestProfile?> Handle(LinkGuestToUserCommand command);
    Task<GuestProfile?> Handle(SetGuestIdentificationCommand command);
    Task<GuestProfile?> Handle(CorrectGuestIdentificationCommand command);
    Task<GuestProfile?> Handle(UpdateGuestContactInformationCommand command);
    Task<GuestProfile?> Handle(DeactivateGuestProfileCommand command);
    Task<GuestProfile?> Handle(ActivateGuestProfileCommand command);
}
