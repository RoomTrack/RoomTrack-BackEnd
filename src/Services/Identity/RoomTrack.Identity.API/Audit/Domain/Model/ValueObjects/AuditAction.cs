namespace BackendAwRoomTrack.API.Audit.Domain.Model.ValueObjects;

/// <summary>Access-related actions recorded in the audit log.</summary>
public enum AuditAction
{
    SignInSucceeded,
    SignInFailed,
    AccountLocked,
    SignedOut,
    PasswordReset,
    PasswordChanged,
    UserCreated,
    RoleChanged,
    UserDeactivated,
    UserActivated,
    MfaEnabled,
    MfaVerified,
    MfaFailed,
    MfaRecoveryCodeUsed,
    MfaReset,
    SignedOutEverywhere,
    AssignmentChanged
}

/// <summary>Whether the recorded action succeeded.</summary>
public enum AuditOutcome
{
    Success,
    Failure
}
