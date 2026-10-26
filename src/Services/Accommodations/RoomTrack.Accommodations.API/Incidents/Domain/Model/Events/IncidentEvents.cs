using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Events;

namespace RoomTrack.Incidents.Domain.Model.Events;

/// <summary>An incident was reported (recorded once the incident has its generated id).</summary>
public sealed record IncidentReportedEvent(int IncidentId, int HotelId, int RoomId, IncidentPriority Priority, bool BlocksRoom,
    int ReportedByUserId, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>An incident was assigned (or reassigned) to a maintenance staff member.</summary>
public sealed record IncidentAssignedEvent(int IncidentId, int HotelId, int AssigneeUserId, int AssignedByUserId,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>The assignee started working on an incident.</summary>
public sealed record IncidentStartedEvent(int IncidentId, int HotelId, int ReportedByUserId, int StartedByUserId,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>An incident was resolved.</summary>
public sealed record IncidentResolvedEvent(int IncidentId, int HotelId, int RoomId, bool BlocksRoom, int ReportedByUserId,
    int ResolvedByUserId, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>A critical incident was not started in time: the hotel administrators are alerted.</summary>
public sealed record IncidentEscalatedEvent(int IncidentId, int HotelId, DateTimeOffset ReportedAt, DateTimeOffset OccurredOn)
    : DomainEvent(OccurredOn);

/// <summary>
///     The urgency of an open incident changed: its assignee is notified, highlighted when it became critical.
/// </summary>
public sealed record IncidentPriorityChangedEvent(int IncidentId, int HotelId, int RoomId, int? AssigneeUserId,
    IncidentPriority FromPriority, IncidentPriority ToPriority, int ChangedByUserId, DateTimeOffset OccurredOn)
    : DomainEvent(OccurredOn);
