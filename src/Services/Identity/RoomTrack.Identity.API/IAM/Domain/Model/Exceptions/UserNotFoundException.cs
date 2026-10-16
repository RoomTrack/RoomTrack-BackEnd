using BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions;

namespace BackendAwRoomTrack.API.IAM.Domain.Model.Exceptions;

public class UserNotFoundException : EntityNotFoundException
{
    public UserNotFoundException(int userId)
        : base("User", userId) { }
}
