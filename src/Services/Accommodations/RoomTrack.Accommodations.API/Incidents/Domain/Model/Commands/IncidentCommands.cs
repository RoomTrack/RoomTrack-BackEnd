using RoomTrack.Incidents.Domain.Model.ValueObjects;

namespace RoomTrack.Incidents.Domain.Model.Commands;

/// <summary>A staff member reports an incident of a room (US-08).</summary>
/// <param name="RoomId">The damaged room.</param>
/// <param name="Title">Short summary, e.g. "Leaking shower".</param>
/// <param name="Description">Details, optional.</param>
/// <param name="Priority">Urgency.</param>
/// <param name="BlocksRoom">The room cannot be used until the incident is resolved.</param>
/// <param name="Reporter">Who reports it.</param>
public record ReportIncidentCommand(int RoomId, string Title, string? Description, IncidentPriority Priority, bool BlocksRoom,
    IncidentActor Reporter);

/// <summary>Assigns an incident to a maintenance staff member of its hotel.</summary>
public record AssignIncidentCommand(int IncidentId, int AssigneeUserId, IncidentActor Actor);

/// <summary>The assignee starts working on an incident. A maintenance staff member starting an unassigned one takes it.</summary>
public record StartIncidentCommand(int IncidentId, IncidentActor Actor);

/// <summary>The assignee resolves an incident, describing what was done.</summary>
public record ResolveIncidentCommand(int IncidentId, string? ResolutionNotes, IncidentActor Actor);

/// <summary>Re-classifies the urgency of an incident.</summary>
public record ChangeIncidentPriorityCommand(int IncidentId, IncidentPriority Priority, IncidentActor Actor);

/// <summary>Alerts the administrators of critical incidents not started in time. Run by the scheduler.</summary>
public record EscalateLateIncidentsCommand;
