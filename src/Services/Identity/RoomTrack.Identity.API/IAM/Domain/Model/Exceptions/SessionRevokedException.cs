using BackendAwRoomTrack.API.IAM.Domain.Model.Enums;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions;

namespace BackendAwRoomTrack.API.IAM.Domain.Model.Exceptions;

/// <summary>
///     The user's sessions were ended (<c>auth.session_revoked</c>): a token issued before can no longer be used or
///     refreshed. <see cref="Reason"/> tells the client why, so it can explain it (e.g. "your permissions changed").
/// </summary>
public class SessionRevokedException(SessionRevocationReason? reason)
    : AuthenticationFailedException(IamErrorCodes.SessionRevoked, SessionRevocationReasons.DetailFor(reason))
{
    /// <summary>Stable reason code (<see cref="SessionRevocationReasons"/>).</summary>
    public string Reason { get; } = SessionRevocationReasons.CodeFor(reason);
}
