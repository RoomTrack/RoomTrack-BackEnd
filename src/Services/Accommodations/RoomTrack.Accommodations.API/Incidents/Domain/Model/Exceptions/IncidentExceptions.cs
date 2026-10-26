using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Incidents.Domain.Model.Exceptions;

/// <summary>Stable error codes of the Incidents context.</summary>
public static class IncidentErrorCodes
{
    public const string InvalidStatusTransition = "incident.invalid_status_transition";
    public const string AlreadyResolved = "incident.already_resolved";
    public const string NotAssigned = "incident.not_assigned";
    public const string NotAssignee = "incident.not_assignee";
    public const string AssignNotAllowed = "incident.assign_not_allowed";
    public const string AssigneeNotMaintenance = "incident.assignee_not_maintenance";
    public const string TitleLength = "incident.title_length";
    public const string DescriptionLength = "incident.description_length";
    public const string ResolutionNotesRequired = "incident.resolution_notes_required";
    public const string ResolutionNotesLength = "incident.resolution_notes_length";
}

/// <summary>The incident is not in a status that allows the operation.</summary>
public class InvalidIncidentStatusTransitionException(int incidentId, IncidentStatus from, string operation)
    : BusinessRuleViolationException(IncidentErrorCodes.InvalidStatusTransition,
        $"Incident {incidentId} cannot be {operation} while it is {from}.");

/// <summary>A resolved incident is closed: it cannot be assigned, re-prioritized or worked on.</summary>
public class IncidentAlreadyResolvedException(int incidentId)
    : BusinessRuleViolationException(IncidentErrorCodes.AlreadyResolved, $"Incident {incidentId} is already resolved.");

/// <summary>The incident has nobody assigned: it must be assigned before work starts.</summary>
public class IncidentNotAssignedException(int incidentId)
    : BusinessRuleViolationException(IncidentErrorCodes.NotAssigned,
        $"Incident {incidentId} has no assignee. Assign it to a maintenance staff member first.");

/// <summary>Only the assignee (or an administrator) works on an incident.</summary>
public class NotIncidentAssigneeException(int incidentId)
    : OperationNotAllowedException(IncidentErrorCodes.NotAssignee,
        $"Incident {incidentId} is assigned to someone else. Only its assignee or an administrator can work on it.");
