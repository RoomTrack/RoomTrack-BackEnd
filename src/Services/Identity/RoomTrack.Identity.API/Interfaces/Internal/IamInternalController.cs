using BackendAwRoomTrack.API.IAM.Application.OutboundServices;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RoomTrack.Identity.API.Interfaces.Internal;

/// <summary>Body of the hotel assignment of an administrator.</summary>
/// <param name="HotelId">The hotel the administrator registered.</param>
/// <param name="RememberedSessionId">Remembered session of the administrator's current token, if any.</param>
public sealed record AssignHotelRequest(int HotelId, Guid? RememberedSessionId);

/// <summary>
///     Internal API of the Identity service (the <see cref="IIamContextFacade"/> port and the session check of the
///     access tokens), called by the other services only. Not routed by the API gateway.
/// </summary>
[ApiController]
[Route("internal/v1")]
[Authorize(Policy = InternalApiServiceCollectionExtensions.InternalServicePolicy)]
[ApiExplorerSettings(IgnoreApi = true)]
public class IamInternalController(IIamContextFacade iamContextFacade, IUserSessionValidator sessionValidator)
    : ControllerBase
{
    [HttpGet("sessions/{userId:int}/{tokenVersion:int}")]
    public async Task<IActionResult> GetSession(int userId, int tokenVersion, CancellationToken cancellationToken) =>
        Ok(await sessionValidator.GetSessionAsync(userId, tokenVersion, cancellationToken));

    [HttpGet("users/{userId:int}/contact")]
    public async Task<IActionResult> GetUserContact(int userId) =>
        await iamContextFacade.FetchUserContactAsync(userId) is { } contact ? Ok(contact) : NotFound();

    [HttpGet("users/{userId:int}/email")]
    public async Task<IActionResult> GetEmail(int userId) => Ok(await iamContextFacade.FetchEmailByUserId(userId));

    [HttpGet("users/id")]
    public async Task<IActionResult> GetUserIdByEmail([FromQuery] string email) =>
        Ok(await iamContextFacade.FetchUserIdByEmail(email));

    [HttpGet("hotels/{hotelId:int}/staff")]
    public async Task<IActionResult> ListHotelStaff(int hotelId, [FromQuery] string[] roles) =>
        Ok(await iamContextFacade.ListHotelStaffAsync(hotelId, roles));

    [HttpPost("users/{userId:int}/hotel-assignment")]
    public async Task<IActionResult> AssignHotel(int userId, AssignHotelRequest request)
    {
        var session = await iamContextFacade.AssignHotelToAdministratorAsync(
            userId, request.HotelId, new SessionContext(request.RememberedSessionId));
        return session is null ? NoContent() : Ok(session);
    }
}
