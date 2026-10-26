using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.IAM.Domain.Model.Exceptions;

public class UserNotFoundException : EntityNotFoundException
{
    public UserNotFoundException(int userId)
        : base("User", userId) { }
}
