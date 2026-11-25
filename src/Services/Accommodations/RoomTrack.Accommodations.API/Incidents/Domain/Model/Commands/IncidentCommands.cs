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

/// <summary>Maintenance or an administrator schedules a recurring preventive task of a room (US-30).</summary>
/// <param name="RoomId">The room.</param>
/// <param name="Title">What to do, e.g. "Clean the air conditioner filters".</param>
/// <param name="Description">Details, optional.</param>
/// <param name="FrequencyDays">Days between occurrences (1-365).</param>
/// <param name="FirstDueOn">When the first task is generated (today or later).</param>
/// <param name="ReminderDaysBefore">Days before each due date the maintenance staff is reminded (0: none).</param>
/// <param name="ScheduledBy">Who schedules it.</param>
public record SchedulePreventiveMaintenanceCommand(int RoomId, string Title, string? Description, int FrequencyDays,
    DateOnly FirstDueOn, int ReminderDaysBefore, IncidentActor ScheduledBy);

/// <summary>Changes the frequency, next due date and reminder of a preventive maintenance plan.</summary>
public record ReschedulePreventiveMaintenanceCommand(int PlanId, int FrequencyDays, DateOnly NextDueOn, int ReminderDaysBefore);

/// <summary>Stops a preventive maintenance plan.</summary>
public record DeactivatePreventiveMaintenanceCommand(int PlanId);

/// <summary>
///     Run by the scheduler (daily): generates the tasks of the plans due today and reminds the maintenance staff of
///     the ones due soon (US-30).
/// </summary>
public record RunPreventiveMaintenanceCommand;
