using System.ComponentModel.DataAnnotations;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Commands;
using RoomTrack.GuestRequests.Domain.Model.Exceptions;
using RoomTrack.GuestRequests.Domain.Model.Queries;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.GuestRequests.Domain.Services;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.GuestRequests.Interfaces.REST;

/// <summary>
///     The catalog of services each hotel offers to its guests, with prices and details (US-33). The guest picks a
///     service when sending a request (<c>POST /guest-requests</c> with <c>serviceItemId</c>).
/// </summary>
[Authorize(Policy = Policies.ReadInventory)]
[ApiController]
[Route("api/v1/service-catalog")]
[Produces("application/json")]
[SwaggerTag("Catalog of services of a hotel for its guests")]
public class ServiceCatalogController(
    IServiceCatalogCommandService commandService,
    IServiceCatalogQueryService queryService,
    IRoomReservationsFacade roomReservationsFacade) : ControllerBase
{
    /// <summary>The services of a hotel, by area and name.</summary>
    /// <remarks>
    ///     A guest sees the available services of the hotel of their stay today (or of <c>hotelId</c>, to browse it before
    ///     arriving); the staff sees every service of their hotel, available or not.
    /// </remarks>
    /// <param name="hotelId">The hotel (optional for a guest staying today and for hotel staff).</param>
    [HttpGet]
    [SwaggerOperation(Summary = "List the service catalog", OperationId = "GetServiceCatalog")]
    [ProducesResponseType(typeof(IEnumerable<ServiceCatalogItemResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        [ { "id": 3, "hotelId": 1, "name": "Desayuno a la habitación", "description": "Continental, de 7:00 a 10:30",
            "area": "Reception", "price": 35.00, "isIncluded": false, "estimatedMinutes": 30, "isAvailable": true },
          { "id": 4, "hotelId": 1, "name": "Toallas extra", "description": null, "area": "Housekeeping", "price": 0,
            "isIncluded": true, "estimatedMinutes": 15, "isAvailable": true } ]
        """)]
    public async Task<IActionResult> GetServiceCatalog([FromQuery] int? hotelId)
    {
        int targetHotelId;
        if (User.IsGuest())
        {
            targetHotelId = hotelId
                            ?? (await roomReservationsFacade.FetchCurrentStayAsync(User.GetUserId()))?.HotelId
                            ?? throw new InvalidFieldException("hotelId", GuestRequestErrorCodes.ServiceHotelRequired,
                                "Send the hotelId of the catalog (you have no stay today).");
        }
        else
        {
            targetHotelId = hotelId ?? User.GetHotelId()
                ?? throw new InvalidFieldException("hotelId", GuestRequestErrorCodes.ServiceHotelRequired,
                    "Send the hotelId of the catalog.");
            if (!InHotel(targetHotelId)) return Forbid();
        }

        var items = await queryService.Handle(new GetServiceCatalogQuery(targetHotelId, OnlyAvailable: User.IsGuest()));
        return Ok(items.Select(ServiceCatalogItemResource.From));
    }

    /// <summary>A service of the catalog.</summary>
    [HttpGet("{serviceItemId:int}")]
    [SwaggerOperation(Summary = "Get a service of the catalog", OperationId = "GetServiceCatalogItem")]
    [ProducesResponseType(typeof(ServiceCatalogItemResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceCatalogItem(int serviceItemId)
    {
        var item = await queryService.Handle(new GetServiceCatalogItemByIdQuery(serviceItemId));
        if (item is null || (User.IsGuest() ? !item.IsAvailable : !InHotel(item.HotelId))) return NotFound();
        return Ok(ServiceCatalogItemResource.From(item));
    }

    /// <summary>Adds a service to the catalog of the hotel.</summary>
    /// <remarks>Administrators of the hotel or chain administrators. A price of 0 means it is included in the stay.</remarks>
    /// <param name="hotelId">The hotel (optional for a hotel administrator; required for a chain admin).</param>
    /// <param name="resource">The service.</param>
    [HttpPost]
    [Authorize(Policy = Policies.ManageHotels)]
    [SwaggerOperation(Summary = "Add a service to the catalog", OperationId = "CreateServiceCatalogItem")]
    [ProducesResponseType(typeof(ServiceCatalogItemResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [OpenApiRequestExample("""
        { "name": "Desayuno a la habitación", "description": "Continental, de 7:00 a 10:30", "area": "Reception",
          "price": 35.00, "estimatedMinutes": 30, "isAvailable": true }
        """)]
    public async Task<IActionResult> CreateServiceCatalogItem([FromQuery] int? hotelId,
        [FromBody] SaveServiceCatalogItemResource resource)
    {
        var targetHotelId = hotelId ?? User.GetHotelId()
            ?? throw new InvalidFieldException("hotelId", GuestRequestErrorCodes.ServiceHotelRequired, "Send the hotelId of the catalog.");
        if (!InHotel(targetHotelId)) return Forbid();
        var item = await commandService.Handle(new CreateServiceCatalogItemCommand(targetHotelId, resource.ToDetails()));
        return CreatedAtAction(nameof(GetServiceCatalogItem), new { serviceItemId = item.Id }, ServiceCatalogItemResource.From(item));
    }

    /// <summary>Changes a service of the catalog.</summary>
    /// <remarks>
    ///     Administrators of its hotel. Send <c>isAvailable: false</c> to stop offering it without removing it. The
    ///     requests already sent keep the name and price they had.
    /// </remarks>
    [HttpPut("{serviceItemId:int}")]
    [Authorize(Policy = Policies.ManageHotels)]
    [SwaggerOperation(Summary = "Update a service of the catalog", OperationId = "UpdateServiceCatalogItem")]
    [ProducesResponseType(typeof(ServiceCatalogItemResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateServiceCatalogItem(int serviceItemId, [FromBody] SaveServiceCatalogItemResource resource)
    {
        var current = await queryService.Handle(new GetServiceCatalogItemByIdQuery(serviceItemId));
        if (current is null || !InHotel(current.HotelId)) return NotFound();
        var item = await commandService.Handle(new UpdateServiceCatalogItemCommand(serviceItemId, resource.ToDetails()));
        return item is null ? NotFound() : Ok(ServiceCatalogItemResource.From(item));
    }

    /// <summary>Removes a service from the catalog (the requests already sent keep its name and price).</summary>
    [HttpDelete("{serviceItemId:int}")]
    [Authorize(Policy = Policies.ManageHotels)]
    [SwaggerOperation(Summary = "Remove a service from the catalog", OperationId = "DeleteServiceCatalogItem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteServiceCatalogItem(int serviceItemId)
    {
        var current = await queryService.Handle(new GetServiceCatalogItemByIdQuery(serviceItemId));
        if (current is null || !InHotel(current.HotelId)) return NotFound();
        return await commandService.Handle(new DeleteServiceCatalogItemCommand(serviceItemId)) ? NoContent() : NotFound();
    }

    /// <summary>Hotel staff operate their own hotel; a chain administrator every hotel.</summary>
    private bool InHotel(int hotelId) => User.IsChainAdmin() || User.GetHotelId() == hotelId;
}

/// <summary>A service of the catalog, as the administrator writes it.</summary>
public record SaveServiceCatalogItemResource
{
    /// <summary>What the guest sees (3-80 characters).</summary>
    /// <example>Desayuno a la habitación</example>
    [Required]
    public string? Name { get; init; }

    /// <summary>Details for the guest (up to 500 characters).</summary>
    /// <example>Continental, de 7:00 a 10:30</example>
    public string? Description { get; init; }

    /// <summary>The area that attends it: Reception, Housekeeping or Maintenance.</summary>
    /// <example>Reception</example>
    [Required]
    public ServiceArea? Area { get; init; }

    /// <summary>Price for the guest; 0 when it is included in the stay.</summary>
    /// <example>35.00</example>
    [Required]
    public decimal? Price { get; init; }

    /// <summary>How long it usually takes, in minutes (1-1440).</summary>
    /// <example>30</example>
    public int? EstimatedMinutes { get; init; }

    /// <summary>Offered to the guests (true by default).</summary>
    public bool IsAvailable { get; init; } = true;

    public ServiceCatalogItemDetails ToDetails() =>
        new(Name, Description, Area!.Value, Price!.Value, EstimatedMinutes, IsAvailable);
}

/// <summary>A service of the catalog of a hotel.</summary>
public record ServiceCatalogItemResource(
    int Id,
    int HotelId,
    string Name,
    string? Description,
    ServiceArea Area,
    decimal Price,
    bool IsIncluded,
    int? EstimatedMinutes,
    bool IsAvailable)
{
    public static ServiceCatalogItemResource From(ServiceCatalogItem item) => new(item.Id, item.HotelId, item.Name,
        item.Description, item.Area, item.Price, item.IsIncluded, item.EstimatedMinutes, item.IsAvailable);
}
