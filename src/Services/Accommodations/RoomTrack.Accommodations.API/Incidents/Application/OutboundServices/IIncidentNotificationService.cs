using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Incidents.Domain.Model.ValueObjects;

namespace RoomTrack.Incidents.Application.OutboundServices;

/// <summary>The incident (and its room) an e-mail is about.</summary>
public sealed record IncidentNotice(int IncidentId, string Title, IncidentPriority Priority, IncidentStatus Status,
    bool BlocksRoom, string RoomNumber, string HotelName, string? ResolutionNotes);

/// <summary>E-mails to the hotel staff about incidents. Enlisted in the outbox inside the transaction of the change.</summary>
public interface IIncidentNotificationService
{
    /// <summary>A new incident: the maintenance staff (and the administrators, if critical) are notified (US-08).</summary>
    Task SendReportedAsync(IReadOnlyList<UserContact> recipients, IncidentNotice incident, string? reportedBy);

    /// <summary>The incident was assigned to the recipient.</summary>
    Task SendAssignedAsync(UserContact assignee, IncidentNotice incident, string? assignedBy);

    /// <summary>The reporter learns that work started on the incident or that it was resolved (US-09).</summary>
    Task SendProgressAsync(UserContact reporter, IncidentNotice incident, string? changedBy);

    /// <summary>A critical incident was not started in time.</summary>
    Task SendEscalatedAsync(IReadOnlyList<UserContact> recipients, IncidentNotice incident, DateTimeOffset reportedAt);
}
