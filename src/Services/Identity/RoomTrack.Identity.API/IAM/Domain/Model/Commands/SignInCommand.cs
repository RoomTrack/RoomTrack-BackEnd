namespace BackendAwRoomTrack.API.IAM.Domain.Model.Commands;

/// <summary>Sign in with e-mail and password.</summary>
/// <param name="Email">Login e-mail.</param>
/// <param name="Password">Password.</param>
/// <param name="RememberMe">Also start a remembered session (refresh token).</param>
public record SignInCommand(string Email, string Password, bool RememberMe = false);
