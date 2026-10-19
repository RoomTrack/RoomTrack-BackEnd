using BackendAwRoomTrack.API.Bookings.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RoomTrack.Bookings.API.Interfaces.Internal;

/// <summary>
///     Internal API of the Bookings service (the <see cref="IRoomReservationsFacade"/> port), called by the
///     Accommodations service. Not routed by the API gateway.
/// </summary>
[ApiController]
[Route("internal/v1")]
[Authorize(Policy = InternalApiServiceCollectionExtensions.InternalServicePolicy)]
[ApiExplorerSettings(IgnoreApi = true)]
public class BookingsInternalController(IRoomReservationsFacade roomReservationsFacade) : ControllerBase
{
    [HttpGet("rooms/active-bookings")]
    public async Task<IActionResult> CountActiveBookings([FromQuery] int[] roomIds) =>
        Ok(await roomReservationsFacade.CountActiveBookingsAsync(roomIds));

    [HttpGet("guests/{guestUserId:int}/current-stay")]
    public async Task<IActionResult> HasCurrentConfirmedStay(int guestUserId, [FromQuery] int roomId, [FromQuery] DateTime day) =>
        Ok(await roomReservationsFacade.HasCurrentConfirmedStayAsync(guestUserId, roomId, day));
}
