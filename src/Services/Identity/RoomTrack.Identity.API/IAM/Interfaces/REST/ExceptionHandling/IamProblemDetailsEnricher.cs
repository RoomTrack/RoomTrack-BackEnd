using BackendAwRoomTrack.API.IAM.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Configuration;
using BackendAwRoomTrack.API.Shared.Infrastructure.Interfaces.ASP.ExceptionHandling;
using Microsoft.Extensions.Options;

namespace BackendAwRoomTrack.API.IAM.Interfaces.REST.ExceptionHandling;

/// <summary>
///     Extra members of the IAM problem responses:
///     <list type="bullet">
///         <item>409 "Email already registered" → <c>passwordRecoveryUrl</c> (suggest recovering the password);</item>
///         <item>401 account locked → <c>lockedUntil</c>;</item>
///         <item>403 e-mail not verified → <c>emailVerificationRequired: true</c>;</item>
///         <item>401 session revoked on refresh → <c>reason</c> (same codes as a revoked bearer token).</item>
///     </list>
/// </summary>
public class IamProblemDetailsEnricher(IOptions<ApplicationUrlsSettings> urls) : IProblemDetailsEnricher
{
    public void Enrich(ProblemDetailsContext context)
    {
        switch (context.Exception)
        {
            case EmailAlreadyRegisteredException alreadyRegistered:
                context.ProblemDetails.Extensions["passwordRecoveryUrl"] =
                    urls.Value.WebLink("forgot-password", ("email", alreadyRegistered.Email));
                break;
            case EmailNotVerifiedException:
                context.ProblemDetails.Extensions["emailVerificationRequired"] = true;
                break;
            case AccountTemporarilyLockedException locked:
                context.ProblemDetails.Extensions["lockedUntil"] = locked.LockedUntil;
                break;
            case SessionRevokedException revoked:
                context.ProblemDetails.Extensions["reason"] = revoked.Reason;
                break;
        }
    }
}
