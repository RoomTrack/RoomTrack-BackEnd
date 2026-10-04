using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RoomTrack.Accommodations.API.Interfaces.Internal;

/// <summary>Body of the occupation of a room by the guest of a completed check-in.</summary>
public sealed record OccupyRoomRequest(int? GuestUserId, string? GuestEmail);

/// <summary>
///     Internal API of the Accommodations service (the <see cref="IAccommodationsContextFacade"/> port), called by the
///     Bookings and Profiles services. Not routed by the API gateway.
/// </summary>
[ApiController]
[Route("internal/v1")]
[Authorize(Policy = InternalApiServiceCollectionExtensions.InternalServicePolicy)]
[ApiExplorerSettings(IgnoreApi = true)]
public class AccommodationsInternalController(IAccommodationsContextFacade accommodationsContextFacade) : ControllerBase
{
    [HttpGet("rooms/numbers")]
    public async Task<IActionResult> GetRoomNumbers([FromQuery] int[] ids) =>
        Ok(await accommodationsContextFacade.FetchRoomNumbersAsync(ids));

    [HttpGet("rooms/{roomId:int}/offer")]
    public async Task<IActionResult> GetRoom(int roomId) =>
        await accommodationsContextFacade.FetchRoomAsync(roomId) is { } room ? Ok(room) : NotFound();

    [HttpGet("rooms/{roomId:int}/exists")]
    public async Task<IActionResult> RoomExists(int roomId) => Ok(await accommodationsContextFacade.RoomExistsAsync(roomId));

    [HttpGet("rooms/offered")]
    public async Task<IActionResult> GetRoomsOffered([FromQuery] int? hotelId) =>
        Ok(await accommodationsContextFacade.FetchRoomsOfferedForBookingAsync(hotelId));

    [HttpPost("rooms/{roomId:int}/occupancy")]
    public async Task<IActionResult> OccupyRoom(int roomId, OccupyRoomRequest request)
    {
        await accommodationsContextFacade.OccupyRoomForCheckInAsync(roomId, request.GuestUserId, request.GuestEmail);
        return NoContent();
    }

    [HttpGet("hotels/{hotelId:int}/summary")]
    public async Task<IActionResult> GetHotel(int hotelId) =>
        await accommodationsContextFacade.FetchHotelAsync(hotelId) is { } hotel ? Ok(hotel) : NotFound();

    [HttpGet("hotels/{hotelId:int}/payment-instructions")]
    public async Task<IActionResult> GetPaymentInstructions(int hotelId) =>
        await accommodationsContextFacade.FetchPaymentInstructionsAsync(hotelId) is { } instructions
            ? Ok(instructions)
            : NotFound();

    [HttpGet("hotels/{hotelId:int}/exists")]
    public async Task<IActionResult> HotelExists(int hotelId) => Ok(await accommodationsContextFacade.HotelExistsAsync(hotelId));
}
