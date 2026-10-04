namespace BackendAwRoomTrack.API.IAM.Domain.Model.Commands;

/// <summary>Exchange a refresh token for a new access token and a new refresh token (rotation).</summary>
public record RefreshSessionCommand(string RefreshToken);

/// <summary>Sign out of a remembered session: its refresh tokens are revoked.</summary>
public record SignOutCommand(string RefreshToken);

/// <summary>Confirm the e-mail with the token of the verification link.</summary>
public record VerifyEmailCommand(string Token);

/// <summary>Send a new verification link to an unverified account (answer never reveals whether it exists).</summary>
public record ResendEmailVerificationCommand(string Email);

/// <summary>Send a password reset link if the e-mail belongs to an account (answer never reveals it).</summary>
public record RequestPasswordRecoveryCommand(string Email);

/// <summary>Set a new password with the token of the reset link.</summary>
public record ResetPasswordCommand(string Token, string NewPassword);
