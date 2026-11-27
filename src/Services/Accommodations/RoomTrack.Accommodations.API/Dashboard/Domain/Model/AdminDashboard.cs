using RoomTrack.Bookings.Interfaces.ACL;

namespace RoomTrack.Dashboard.Domain.Model;

// ── What the panel reads (snapshots of the operational contexts) ──────────

/// <summary>A room and its status now.</summary>
public sealed record RoomSnapshot(int RoomId, string Number, string Status);

/// <summary>A cleaning task still open.</summary>
public sealed record OpenCleaningTask(int TaskId, int RoomId, string Priority, int? AssigneeUserId, DateTimeOffset CreatedAt);

/// <summary>An incident still open.</summary>
public sealed record OpenIncident(int IncidentId, int RoomId, string Title, string Priority, bool BlocksRoom,
    DateTimeOffset ReportedAt, DateTimeOffset? EscalatedAt, bool IsRecurring);

/// <summary>A guest request still open.</summary>
public sealed record OpenGuestRequest(int RequestId, int RoomId, string Title, string Area, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset EstimatedAttentionBy);

/// <summary>The operational state of a hotel now.</summary>
public sealed record HotelOperations(
    IReadOnlyList<RoomSnapshot> Rooms,
    IReadOnlyList<OpenCleaningTask> CleaningTasks,
    IReadOnlyList<OpenIncident> Incidents,
    IReadOnlyList<OpenGuestRequest> GuestRequests);

// ── What the panel shows ──────────────────────────────────────────────────

/// <summary>The rooms of the hotel by status.</summary>
/// <param name="Total">All the rooms of the hotel.</param>
/// <param name="Available">Clean and free.</param>
/// <param name="Occupied">With a guest staying.</param>
/// <param name="Cleaning">Being cleaned (pending cleaning after a stay).</param>
/// <param name="Maintenance">Out of service.</param>
/// <param name="OccupancyPercent">Occupied over all the rooms, 0-100.</param>
public sealed record RoomsSummary(int Total, int Available, int Occupied, int Cleaning, int Maintenance, double OccupancyPercent);

/// <summary>The cleaning tasks still open.</summary>
/// <param name="Open">Pending or in progress.</param>
/// <param name="Unassigned">Open without a housekeeper.</param>
/// <param name="Overdue">Open for <paramref name="OverdueAfterMinutes"/> or more since they were created.</param>
/// <param name="OverdueAfterMinutes">When an open task counts as overdue.</param>
public sealed record CleaningSummary(int Open, int Unassigned, int Overdue, int OverdueAfterMinutes);

/// <summary>The incidents still open.</summary>
/// <param name="Open">Pending or in progress.</param>
/// <param name="Critical">Open with Critical priority.</param>
/// <param name="High">Open with High priority.</param>
/// <param name="BlockingRooms">Open ones that keep their room out of service.</param>
/// <param name="Escalated">Critical ones nobody started in time (US-23).</param>
/// <param name="Recurring">Part of a pattern in their room (US-10).</param>
public sealed record IncidentsSummary(int Open, int Critical, int High, int BlockingRooms, int Escalated, int Recurring);

/// <summary>The guest requests still open.</summary>
/// <param name="Open">Pending or in progress.</param>
/// <param name="Pending">Nobody started them yet.</param>
/// <param name="Late">Past the attention time promised to the guest.</param>
public sealed record GuestRequestsSummary(int Open, int Pending, int Late);

/// <summary>Something the administrator should look at first.</summary>
/// <param name="Kind">Incident or GuestRequest.</param>
/// <param name="ReferenceId">Its id (<c>/incidents/{id}</c> or <c>/guest-requests/{id}</c>).</param>
/// <param name="RoomId">The room.</param>
/// <param name="RoomNumber">Its number.</param>
/// <param name="Title">What happens.</param>
/// <param name="Reason">Critical, Escalated or Late.</param>
/// <param name="Since">When it was reported, or when its attention time ran out.</param>
public sealed record AttentionItem(string Kind, int ReferenceId, int RoomId, string? RoomNumber, string Title, string Reason,
    DateTimeOffset Since);

/// <summary>
///     US-18: the administrator's panel of a hotel, in one answer: rooms, cleaning, incidents, guest requests, the front
///     desk of today with the latest evaluations, and what needs attention first.
/// </summary>
/// <param name="HotelId">The hotel.</param>
/// <param name="GeneratedAt">When the panel was calculated.</param>
/// <param name="Rooms">The rooms by status.</param>
/// <param name="Cleaning">The open cleaning tasks.</param>
/// <param name="Incidents">The open incidents.</param>
/// <param name="GuestRequests">The open guest requests.</param>
/// <param name="FrontDesk">From the Bookings service; null when it could not be reached (the rest is still valid).</param>
/// <param name="Attention">What to look at first.</param>
public sealed record AdminDashboard(
    int HotelId,
    DateTimeOffset GeneratedAt,
    RoomsSummary Rooms,
    CleaningSummary Cleaning,
    IncidentsSummary Incidents,
    GuestRequestsSummary GuestRequests,
    FrontDeskSummary? FrontDesk,
    IReadOnlyList<AttentionItem> Attention);
