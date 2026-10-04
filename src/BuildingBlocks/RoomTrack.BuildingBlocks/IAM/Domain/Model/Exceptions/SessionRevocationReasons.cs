using BackendAwRoomTrack.API.IAM.Domain.Model.Enums;

namespace BackendAwRoomTrack.API.IAM.Domain.Model.Exceptions;

/// <summary>Stable codes of <see cref="SessionRevocationReason"/>, as written in the <c>reason</c> of a 401.</summary>
public static class SessionRevocationReasons
{
    public const string RoleChanged = "role_changed";
    public const string AssignmentChanged = "assignment_changed";
    public const string PasswordChanged = "password_changed";
    public const string PasswordReset = "password_reset";
    public const string SignedOutEverywhere = "signed_out_everywhere";
    public const string Deactivated = "deactivated";
    public const string MfaReset = "mfa_reset";

    /// <summary>The account no longer exists, or the sessions ended before reasons were recorded.</summary>
    public const string Unknown = "unknown";

    public static string CodeFor(SessionRevocationReason? reason) => reason switch
    {
        SessionRevocationReason.RoleChanged => RoleChanged,
        SessionRevocationReason.AssignmentChanged => AssignmentChanged,
        SessionRevocationReason.PasswordChanged => PasswordChanged,
        SessionRevocationReason.PasswordReset => PasswordReset,
        SessionRevocationReason.SignedOutEverywhere => SignedOutEverywhere,
        SessionRevocationReason.Deactivated => Deactivated,
        SessionRevocationReason.MfaReset => MfaReset,
        _ => Unknown
    };

    public static string DetailFor(SessionRevocationReason? reason) => reason switch
    {
        SessionRevocationReason.RoleChanged or SessionRevocationReason.AssignmentChanged =>
            "Your permissions changed and your session was closed. Sign in again.",
        SessionRevocationReason.Deactivated => "The account has been deactivated. Contact the administrator.",
        _ => "Your session was closed. Sign in again."
    };
}
