using RoomTrack.GuestRequests.Domain.Model.ValueObjects;

namespace RoomTrack.GuestRequests.Domain.Model.Commands;

/// <summary>The guest sends a request from the app during their stay (US-19); it arrives at the front desk unless they chose the area.</summary>
public record SendGuestRequestCommand(string Title, string? Description, ServiceArea? Area, GuestRequestActor Guest);

/// <summary>The front desk registers what a guest of a room asked for and derives it to an area (US-11).</summary>
public record RegisterGuestRequestCommand(int RoomId, string Title, string? Description, ServiceArea Area, GuestRequestActor FrontDesk);

public record RouteGuestRequestCommand(int GuestRequestId, ServiceArea Area, GuestRequestActor Actor);

public record AssignGuestRequestCommand(int GuestRequestId, int AssigneeUserId, GuestRequestActor Actor);

public record StartGuestRequestCommand(int GuestRequestId, GuestRequestActor Actor);

public record ResolveGuestRequestCommand(int GuestRequestId, string? Note, GuestRequestActor Actor);

public record CancelGuestRequestCommand(int GuestRequestId, string? Reason, GuestRequestActor Actor);
