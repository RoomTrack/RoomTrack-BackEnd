namespace BackendAwRoomTrack.API.IAM.Domain.Model.Exceptions;

/// <summary>
///     Stable codes of the 401/403 answers written by the JWT bearer events of every service (the rest of the IAM codes
///     live in the Identity service).
/// </summary>
public static class BearerErrorCodes
{
    public const string TokenMissing = "auth.token_missing";
    public const string TokenInvalid = "auth.token_invalid";
    public const string TokenExpired = "auth.token_expired";
    public const string SessionRevoked = "auth.session_revoked";
    public const string Forbidden = "auth.forbidden";
}
