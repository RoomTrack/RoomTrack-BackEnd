using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Accommodations.Domain.Model.Exceptions;
using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Domain.Model.Queries;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.Accommodations.Interfaces.REST.Authorization;
using RoomTrack.Accommodations.Interfaces.REST.Resources;
using RoomTrack.Accommodations.Interfaces.REST.Transform;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Accommodations.Application.Internal.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;

namespace RoomTrack.Accommodations.Interfaces.REST;

/// <summary>
///     RESTful API interface controller responsible for handling operational, guest, and administrative 
///     requests related to individual room aggregate roots within the accommodation bounded context.
/// </summary>
[Authorize(Policy = Policies.ReadInventory)]
[ApiController]
[Route("api/v1/[controller]")]
[SwaggerTag("Available Room Endpoints")]
public class RoomsController(
    IRoomCommandService roomCommandService,
    IRoomQueryService roomQueryService,
    IHotelQueryService hotelQueryService,
    IAuthorizationService authorizationService,
    IOptions<RoomOperationsSettings> roomSettings,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    ///     Retrieves a single room resource partition by its structural domain identity marker.
    /// </summary>
    /// <param name="roomId">The unique domain identifier value representing the targeted room aggregate root.</param>
    /// <returns>An asynchronous action result containing the matching room resource state representation.</returns>
    [HttpGet("{roomId:int}")]
    [SwaggerOperation(
        Summary = "Get room by its unique identifier",
        Description = "Retrieves state parameters and specifications for a single room aggregate entry.",
        OperationId = "GetRoomById")]
    [SwaggerResponse(StatusCodes.Status200OK, "The room aggregate was located and converted successfully.", typeof(RoomResource))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The request lacks a valid identity identification token.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "The authenticated identity has insufficient privilege levels.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "No room aggregate matched the supplied structural identifier.")]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "id": 101, "hotelId": 1, "roomTypeId": 2, "roomTypeName": "Doble", "price": 180.00,
        "description": "Doble con vista al parque", "amenities": ["WiFi", "TV", "Aire acondicionado"], "status": "Available", "number": "204" }
        """)]
    public async Task<IActionResult> GetRoomById(int roomId)
    {
        var getRoomByIdQuery = new GetRoomByIdQuery(roomId);
        var room = await roomQueryService.Handle(getRoomByIdQuery);
        if (room is null) return NotFound();
        var resource = RoomResourceFromEntityAssembler.ToResourceFromEntity(room);
        return Ok(resource);
    }

    /// <summary>
    ///     Creates a new room aggregate root inside the property tracking persistence subsystem.
    /// </summary>
    /// <param name="resource">The incoming input resource containing constraints and associations required for construction.</param>
    /// <returns>A created resource response alongside the tracking location parameters of the processed aggregate.</returns>
    [HttpPost]
    [Authorize(Policy = Policies.ManageHotels)]
    [SwaggerOperation(
        Summary = "Create a room",
        Description = ". Admin: rooms of their own hotel; chain_admin: any hotel. The room number is unique in the hotel (409 otherwise), the price per night must be greater than 0 and every missing or invalid field is reported in `errors` (number, roomTypeId, price, description, hotelId). New rooms start Available.",
        OperationId = "CreateRoom")]
    [SwaggerResponse(StatusCodes.Status201Created, "The room aggregate root was successfully processed and initialized.", typeof(RoomResource))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The provided construction resource layout contains invalid fields or broken constraints.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The request lacks a valid identity identification token.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Access denied. Only Admin or ChainAdmin entities are cleared to mutate property assets.")]
    [OpenApiRequestExample("""
        { "hotelId": 1, "number": "204", "roomTypeId": 2, "price": 180.00, "description": "Doble con vista al parque",
        "amenities": ["WiFi", "TV", "Aire acondicionado"] }
        """)]
    [OpenApiResponseExample(StatusCodes.Status201Created, """
        { "id": 101, "hotelId": 1, "roomTypeId": 2, "roomTypeName": "Doble", "price": 180.00,
        "description": "Doble con vista al parque", "amenities": ["WiFi", "TV", "Aire acondicionado"], "status": "Available", "number": "204" }
        """)]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomResource resource)
    {
        var hotel = await hotelQueryService.Handle(new GetHotelByIdQuery(resource.HotelId));
        if (hotel is null)
            throw new InvalidFieldException(nameof(resource.HotelId), AccommodationErrorCodes.HotelNotFound,
                $"Hotel {resource.HotelId} does not exist.");
        if (!await CanManageAsync(hotel)) return Forbid();

        var createRoomCommand = CreateRoomCommandFromResourceAssembler.ToCommandFromResource(resource);
        var room = await roomCommandService.Handle(createRoomCommand);
        if (room is null) return BadRequest();
        var roomResource = RoomResourceFromEntityAssembler.ToResourceFromEntity(room);
        return CreatedAtAction(nameof(GetRoomById), new { roomId = room.Id }, roomResource);
    }

    /// <summary>
    ///     Retrieves an enumerable collection of all active room aggregate resources.
    /// </summary>
    /// <returns>A resource collection mapping all room aggregates present in the persistent tier.</returns>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all registered rooms",
        Description = "Retrieves all room aggregate node instances across properties and transforms them into view resources.",
        OperationId = "GetAllRooms")]
    [SwaggerResponse(StatusCodes.Status200OK, "The room resource list was fetched successfully.", typeof(IEnumerable<RoomResource>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The request lacks a valid identity identification token.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "The authenticated identity has insufficient privilege levels.")]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        [ { "id": 101, "hotelId": 1, "roomTypeId": 2, "roomTypeName": "Doble", "price": 180.00,
        "description": "Doble con vista al parque", "amenities": ["WiFi", "TV"], "status": "Occupied", "number": "204" },
        { "id": 102, "hotelId": 1, "roomTypeId": 3, "roomTypeName": "Suite", "price": 320.00,
        "description": "Suite con jacuzzi", "amenities": ["WiFi", "TV", "Jacuzzi"], "status": "Cleaning", "number": "301" } ]
        """)]
    public async Task<IActionResult> GetAllRooms()
    {
        var rooms = await roomQueryService.Handle(new GetAllRoomsQuery());
        var roomResources = rooms.Select(RoomResourceFromEntityAssembler.ToResourceFromEntity);
        return Ok(roomResources);
    }
    
    /// <summary>
    ///     Filters and extracts a sub-collection of room resources associated with a specific structural type identifier.
    /// </summary>
    /// <param name="roomTypeId">The tracking domain identity marker of the target room type entity.</param>
    /// <returns>An enumerable resource listing matching room representations.</returns>
    [HttpGet("type/{roomTypeId:int}")]
    [SwaggerOperation(
        Summary = "Get rooms by their category or room type",
        Description = "Retrieves a sub-set of room aggregates filtering criteria by their associated category index mapping.",
        OperationId = "GetRoomsByType")]
    [SwaggerResponse(StatusCodes.Status200OK, "The filtered room resource sub-list was fetched successfully.", typeof(IEnumerable<RoomResource>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The request lacks a valid identity identification token.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "The authenticated identity has insufficient privilege levels.")]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        [ { "id": 101, "hotelId": 1, "roomTypeId": 2, "roomTypeName": "Doble", "price": 180.00,
        "description": "Doble con vista al parque", "amenities": ["WiFi", "TV"], "status": "Available", "number": "204" } ]
        """)]
    public async Task<IActionResult> GetRoomsByType(int roomTypeId)
    {
        var rooms = await roomQueryService.Handle(new GetRoomsByTypeQuery(roomTypeId));
        var roomResources = rooms.Select(RoomResourceFromEntityAssembler.ToResourceFromEntity);
        return Ok(roomResources);
    }
    
    /// <summary>
    ///     Updates the internal state representation details of an active room aggregate root.
    /// </summary>
    /// <param name="roomId">The tracking aggregate key pointing to the target room undergoing mutation.</param>
    /// <param name="resource">The incoming state modification layout resource payload.</param>
    /// <returns>The newly updated room representation layout outcome.</returns>
    [HttpPut("{roomId:int}")]
    [Authorize(Policy = Policies.ManageHotels)]
    [SwaggerOperation(
        Summary = "Update an existing room aggregate's context properties",
        Description = ". Changes type, price, description, amenities and optionally the number (unique in the hotel). A new price only applies to new bookings: existing bookings keep the price per night they were made at.",
        OperationId = "UpdateRoom")]
    [SwaggerResponse(StatusCodes.Status200OK, "The room aggregate state was updated successfully.", typeof(RoomResource))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The request lacks a valid identity identification token.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "The requesting identity lacks administrative authorization parameters.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The targeted room aggregate node could not be pulled for state alteration.")]
    [OpenApiRequestExample("""
        { "roomTypeId": 2, "price": 195.00, "description": "Doble con vista al parque, renovada", "amenities": ["WiFi", "TV", "Minibar"],
        "number": "204" }
        """)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "id": 101, "hotelId": 1, "roomTypeId": 2, "roomTypeName": "Doble", "price": 195.00,
        "description": "Doble con vista al parque, renovada", "amenities": ["WiFi", "TV", "Minibar"], "status": "Available", "number": "204" }
        """)]
    public async Task<IActionResult> UpdateRoom(int roomId, [FromBody] UpdateRoomResource resource)
    {
        var notAllowed = await EnsureCanManageRoomAsync(roomId);
        if (notAllowed is not null) return notAllowed;

        var command = UpdateRoomCommandFromResourceAssembler.ToCommandFromResource(roomId, resource);
        var updatedRoom = await roomCommandService.Handle(command);

        if (updatedRoom is null) return NotFound();

        var roomResource = RoomResourceFromEntityAssembler.ToResourceFromEntity(updatedRoom);
        return Ok(roomResource);
    }

    /// <summary>
    ///     Deletes an active room aggregate root tracking node from the transaction persistence engine.
    /// </summary>
    /// <param name="roomId">The unique domain root aggregate identifier targeted for operational removal.</param>
    /// <returns>The final detached state representation data layout of the processed room entry node.</returns>
    [HttpDelete("{roomId:int}")]
    [Authorize(Policy = Policies.ManageHotels)]
    [SwaggerOperation(
        Summary = "Delete a room entity entry",
        Description = ". A room with active bookings (pending, confirmed or checked in) cannot be deleted: 409 with the number of bookings to cancel or move first.",
        OperationId = "DeleteRoom")]
    [SwaggerResponse(StatusCodes.Status200OK, "The room aggregate instance was successfully cleared and decommissioned from the asset cluster.", typeof(RoomResource))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The request lacks a valid identity identification token.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "The requesting entity lacks the high administrative clearance levels required to execute asset deletion.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The targeted room asset node was not present in the structural system cluster tree.")]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "id": 101, "hotelId": 1, "roomTypeId": 2, "roomTypeName": "Doble", "price": 180.00,
        "description": "Doble con vista al parque", "amenities": [], "status": "Available", "number": "204" }
        """)]
    public async Task<IActionResult> DeleteRoom(int roomId)
    {
        var notAllowed = await EnsureCanManageRoomAsync(roomId);
        if (notAllowed is not null) return notAllowed;

        var command = new DeleteRoomCommand(roomId);
        var deletedRoom = await roomCommandService.Handle(command);

        if (deletedRoom is null) return NotFound();

        var roomResource = RoomResourceFromEntityAssembler.ToResourceFromEntity(deletedRoom);
        return Ok(roomResource);
    }

    /// <summary>Changes the operational status of a room.</summary>
    /// <remarks>
    ///     The change is recorded in the room's status history (who and when) and the staff in charge of the new
    ///     status is notified by e-mail: Cleaning and Occupied → housekeeping, Maintenance → maintenance and the hotel
    ///     admin, Available → reception.
    ///     Valid transitions: Available → Occupied, Cleaning, Maintenance; Occupied → Cleaning, Maintenance;
    ///     Cleaning → Available, Maintenance; Maintenance → Available, Cleaning. Setting the current status is a no-op.
    ///     Hotel staff change the rooms of their hotel; a chain admin any room. PUT is accepted as a synonym.
    /// </remarks>
    [HttpPatch("{roomId:int}/status")]
    [HttpPut("{roomId:int}/status")]
    [Authorize(Policy = Policies.UpdateRoomStatus)]
    [SwaggerOperation(Summary = "Change the status of a room", OperationId = "ChangeRoomStatus")]
    [ProducesResponseType(typeof(RoomResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "status": "Cleaning" }""")]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "id": 101, "hotelId": 1, "roomTypeId": 2, "roomTypeName": "Doble", "price": 180.00,
        "description": "Doble con vista al parque", "amenities": ["WiFi", "TV"], "status": "Cleaning", "number": "204" }
        """)]
    [OpenApiResponseExample(StatusCodes.Status409Conflict, """
        { "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10", "title": "Business rule violation", "status": 409,
        "code": "room.invalid_status_transition", "detail": "Room 101 cannot change from Occupied to Available. Allowed from Occupied: Cleaning, Maintenance." }
        """)]
    public async Task<IActionResult> ChangeRoomStatus(int roomId, [FromBody] ChangeRoomStatusResource resource)
    {
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(roomId));
        if (room is null) return NotFound();
        if (!(await authorizationService.AuthorizeAsync(User, room, RoomOperationsRequirement.Instance)).Succeeded)
            return Forbid();

        var updated = await roomCommandService.Handle(new ChangeRoomStatusCommand(roomId, resource.ToRoomStatus(),
            User.GetUserId(), User.GetUsername()));
        return updated is null ? NotFound() : Ok(RoomResourceFromEntityAssembler.ToResourceFromEntity(updated));
    }

    /// <summary>Room map of a hotel: every room with its status, for the color-coded view.</summary>
    /// <remarks>
    ///     Hotel staff (reception, housekeeping, maintenance, admin) see their hotel (<c>hotelId</c> optional); a chain
    ///     admin must send <c>hotelId</c>. Each room has <c>statusSince</c> and <c>maintenanceOverdue</c> (under
    ///     maintenance for longer than the alert threshold, 24 h). Suggested colors: Available green, Occupied blue,
    ///     Cleaning amber, Maintenance red.
    /// </remarks>
    /// <param name="hotelId">The hotel.</param>
    [HttpGet("map")]
    [Authorize(Policy = Policies.ViewRoomOperations)]
    [SwaggerOperation(Summary = "Room map of a hotel", OperationId = "GetRoomMap")]
    [ProducesResponseType(typeof(RoomMapResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "hotelId": 1, "hotelName": "Hotel Miraflores", "generatedAt": "2026-11-24T20:00:00+00:00",
        "summary": { "total": 2, "available": 0, "occupied": 1, "cleaning": 1, "maintenance": 0, "occupancyPercent": 50.0 },
        "rooms": [ { "id": 101, "number": "204", "roomTypeName": "Doble", "description": "Doble con vista al parque", "price": 180.00,
        "status": "Occupied", "statusSince": "2026-11-24T15:05:00+00:00", "maintenanceOverdue": false,
        "allowedNextStatuses": ["Cleaning", "Maintenance"], "blockedByIncident": false },
        { "id": 102, "number": "301", "roomTypeName": "Suite", "description": "Suite con jacuzzi", "price": 320.00,
        "status": "Cleaning", "statusSince": "2026-11-24T12:20:00+00:00", "maintenanceOverdue": false,
        "allowedNextStatuses": ["Available", "Maintenance"], "blockedByIncident": false } ] }
        """)]
    public async Task<IActionResult> GetRoomMap([FromQuery] int? hotelId)
    {
        var targetHotelId = hotelId ?? (User.IsChainAdmin() ? null : User.GetHotelId());
        if (targetHotelId is null)
            throw new InvalidFieldException("hotelId", AccommodationErrorCodes.HotelRequired, "Send the hotelId of the map.");

        var hotel = await hotelQueryService.Handle(new GetHotelByIdQuery(targetHotelId.Value));
        if (hotel is null) return NotFound();
        if (!User.IsChainAdmin() && User.GetHotelId() != hotel.Id) return Forbid();

        var rooms = await roomQueryService.Handle(new GetRoomMapQuery(hotel.Id));
        return Ok(RoomMapResourceAssembler.ToResource(hotel, rooms, timeProvider.GetUtcNow(), roomSettings.Value.MaintenanceAlertAfter));
    }

    /// <summary>Status history of a room, newest first: date, time and user of each change.</summary>
    [HttpGet("{roomId:int}/status-history")]
    [Authorize(Policy = Policies.ViewRoomOperations)]
    [SwaggerOperation(Summary = "Status history of a room", OperationId = "GetRoomStatusHistory")]
    [ProducesResponseType(typeof(IEnumerable<RoomStatusChangeResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        [ { "id": 310, "roomId": 101, "fromStatus": "Available", "toStatus": "Occupied", "origin": "CheckIn",
        "changedAt": "2026-11-24T15:05:00+00:00", "changedByUserId": 25, "changedByEmail": "ana.torres@example.com" },
        { "id": 298, "roomId": 101, "fromStatus": "Cleaning", "toStatus": "Available", "origin": "Staff",
        "changedAt": "2026-11-24T11:40:00+00:00", "changedByUserId": 14, "changedByEmail": "rosa.quispe@roomtrack.pe" } ]
        """)]
    public async Task<IActionResult> GetStatusHistory(int roomId)
    {
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(roomId));
        if (room is null) return NotFound();
        if (!(await authorizationService.AuthorizeAsync(User, room, RoomOperationsRequirement.Instance)).Succeeded)
            return Forbid();

        var history = await roomQueryService.Handle(new GetRoomStatusHistoryQuery(roomId));
        return Ok(history.Select(RoomMapResourceAssembler.ToResource));
    }

    /// <summary>
    ///     Resource-based authorization on the room's hotel: 404 when the room does not exist, 403 (native Forbid)
    ///     when its hotel is outside the requester's scope, null when the requester may manage it.
    /// </summary>
    private async Task<IActionResult?> EnsureCanManageRoomAsync(int roomId)
    {
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(roomId));
        if (room is null) return NotFound();

        var hotel = await hotelQueryService.Handle(new GetHotelByIdQuery(room.HotelId));
        if (hotel is null) return User.IsChainAdmin() ? null : Forbid();

        return await CanManageAsync(hotel) ? null : Forbid();
    }

    private async Task<bool> CanManageAsync(Hotel hotel) =>
        (await authorizationService.AuthorizeAsync(User, hotel, HotelManagementRequirement.Instance)).Succeeded;
}
