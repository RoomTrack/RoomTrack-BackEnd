using RoomTrack.GuestRequests.Domain.Model.ValueObjects;

namespace RoomTrack.GuestRequests.Domain.Model.Queries;

public record GetGuestRequestByIdQuery(int GuestRequestId);

/// <summary>The requests of a hotel (staff), newest first, optionally of one area, status, room or assignee.</summary>
public record GetGuestRequestsQuery(int HotelId, ServiceArea? Area = null, GuestRequestStatus? Status = null, int? RoomId = null,
    int? AssigneeUserId = null, bool OnlyOpen = false, int Limit = 100);

/// <summary>The requests a guest sent or that were registered for them, newest first.</summary>
public record GetGuestRequestsOfGuestQuery(int GuestUserId, int Limit = 100);

/// <summary>The service catalog of a hotel, by area and name (US-33); only the available services for the guests.</summary>
public record GetServiceCatalogQuery(int HotelId, bool OnlyAvailable);

public record GetServiceCatalogItemByIdQuery(int ServiceItemId);
