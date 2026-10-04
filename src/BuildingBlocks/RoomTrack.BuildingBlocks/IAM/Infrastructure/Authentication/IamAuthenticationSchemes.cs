using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace BackendAwRoomTrack.API.IAM.Infrastructure.Authentication;

/// <summary>Authentication schemes of the IAM context.</summary>
public static class IamAuthenticationSchemes
{
    /// <summary>Access tokens (default scheme).</summary>
    public const string Bearer = JwtBearerDefaults.AuthenticationScheme;

    /// <summary>
    ///     Second-factor challenge tokens, also sent as <c>Authorization: Bearer</c> but with their own
    ///     audience. Only the second-factor endpoints authenticate with this scheme.
    /// </summary>
    public const string MfaChallenge = "MfaChallenge";
}
