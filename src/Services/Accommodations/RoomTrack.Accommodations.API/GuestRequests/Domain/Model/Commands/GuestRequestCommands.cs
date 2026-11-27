using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;

namespace RoomTrack.GuestRequests.Domain.Model.Commands;

/// <summary>
///     The guest sends a request from the app during their stay (US-19); it arrives at the front desk unless they chose
///     the area. With <paramref name="ServiceItemId"/> it asks for a service of the catalog (US-33): it goes to the area
///     of the service, and the title defaults to its name.
/// </summary>
public record SendGuestRequestCommand(string? Title, string? Description, ServiceArea? Area, GuestRequestActor Guest,
    int? ServiceItemId = null);

/// <summary>
///     The front desk registers what a guest of a room asked for and derives it to an area (US-11), optionally a service
///     of the catalog (US-33).
/// </summary>
public record RegisterGuestRequestCommand(int RoomId, string? Title, string? Description, ServiceArea Area,
    GuestRequestActor FrontDesk, int? ServiceItemId = null);

/// <summary>The administrator adds a service to the catalog of their hotel (US-33).</summary>
public record CreateServiceCatalogItemCommand(int HotelId, ServiceCatalogItemDetails Details);

/// <summary>The administrator changes a service of the catalog (US-33).</summary>
public record UpdateServiceCatalogItemCommand(int ServiceItemId, ServiceCatalogItemDetails Details);

/// <summary>The administrator removes a service from the catalog; the requests already sent keep its name and price.</summary>
public record DeleteServiceCatalogItemCommand(int ServiceItemId);

public record RouteGuestRequestCommand(int GuestRequestId, ServiceArea Area, GuestRequestActor Actor);

public record AssignGuestRequestCommand(int GuestRequestId, int AssigneeUserId, GuestRequestActor Actor);

public record StartGuestRequestCommand(int GuestRequestId, GuestRequestActor Actor);

public record ResolveGuestRequestCommand(int GuestRequestId, string? Note, GuestRequestActor Actor);

public record CancelGuestRequestCommand(int GuestRequestId, string? Reason, GuestRequestActor Actor);
