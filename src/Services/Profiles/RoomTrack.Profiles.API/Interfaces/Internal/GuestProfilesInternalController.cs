using BackendAwRoomTrack.API.Profiles.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RoomTrack.Profiles.API.Interfaces.Internal;

/// <summary>Body of the creation of a guest profile by another service.</summary>
public sealed record CreateGuestProfileRequest(string FirstName, string LastName, string Phone, string? Email, int? UserId);

/// <summary>Body of the link between a guest profile and a user account.</summary>
public sealed record LinkGuestProfileRequest(int UserId, string Email);

/// <summary>
///     Internal API of the Profiles service (the <see cref="IGuestProfilesContextFacade"/> port), called by the
///     Bookings service. Not routed by the API gateway.
/// </summary>
[ApiController]
[Route("internal/v1/guest-profiles")]
[Authorize(Policy = InternalApiServiceCollectionExtensions.InternalServicePolicy)]
[ApiExplorerSettings(IgnoreApi = true)]
public class GuestProfilesInternalController(IGuestProfilesContextFacade guestProfilesContextFacade) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateGuestProfileRequest request) =>
        await guestProfilesContextFacade.CreateGuestProfileAsync(
            request.FirstName, request.LastName, request.Phone, request.Email, request.UserId) is { } id
            ? Ok(id)
            : NoContent();

    [HttpGet("by-user/{userId:int}")]
    public async Task<IActionResult> FindByUser(int userId) =>
        await guestProfilesContextFacade.FetchGuestProfileIdByUserIdAsync(userId) is { } id ? Ok(id) : NotFound();

    [HttpGet("by-email")]
    public async Task<IActionResult> FindByEmail([FromQuery] string email) =>
        await guestProfilesContextFacade.FetchGuestProfileIdByEmailAsync(email) is { } id ? Ok(id) : NotFound();

    [HttpPost("{guestProfileId:guid}/link")]
    public async Task<IActionResult> Link(Guid guestProfileId, LinkGuestProfileRequest request) =>
        Ok(await guestProfilesContextFacade.LinkGuestProfileToUserAsync(guestProfileId, request.UserId, request.Email));
}
