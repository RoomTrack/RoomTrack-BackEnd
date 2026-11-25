using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Commands;
using RoomTrack.Incidents.Domain.Model.Queries;

namespace RoomTrack.Incidents.Domain.Services;

public interface IIncidentCommandService
{
    /// <summary>Reports an incident; a blocking one takes its room out of service in the same transaction.</summary>
    /// <exception cref="RoomTrack.Shared.Domain.Model.Exceptions.EntityNotFoundException">The room does not exist.</exception>
    Task<Incident> Handle(ReportIncidentCommand command);

    /// <returns>The incident, or null when it does not exist.</returns>
    Task<Incident?> Handle(AssignIncidentCommand command);

    /// <returns>The incident, or null when it does not exist.</returns>
    Task<Incident?> Handle(StartIncidentCommand command);

    /// <summary>Resolves an incident; the room of the last blocking incident is released to cleaning.</summary>
    /// <returns>The incident, or null when it does not exist.</returns>
    Task<Incident?> Handle(ResolveIncidentCommand command);

    /// <returns>The incident, or null when it does not exist.</returns>
    Task<Incident?> Handle(ChangeIncidentPriorityCommand command);

    /// <returns>How many incidents were escalated.</returns>
    Task<int> Handle(EscalateLateIncidentsCommand command);
}

/// <summary>An automatic alert of an incident (US-23) and how it ended.</summary>
/// <param name="Kind">Escalation (critical, not attended in time) or Recurrence (similar incidents of the room this month).</param>
/// <param name="RaisedAt">When the alert was raised.</param>
/// <param name="Incident">The incident: its status and resolution say how the alert was resolved.</param>
public record IncidentAlert(string Kind, DateTimeOffset RaisedAt, Incident Incident);

public interface IIncidentQueryService
{
    Task<Incident?> Handle(GetIncidentByIdQuery query);

    /// <summary>The automatic alerts of the hotel in the period, newest first (US-23).</summary>
    Task<IReadOnlyList<IncidentAlert>> Handle(GetIncidentAlertsQuery query);

    Task<IReadOnlyList<Incident>> Handle(GetIncidentsQuery query);
}

/// <summary>Result of a run of the preventive maintenance job.</summary>
/// <param name="Generated">Tasks generated (incidents of kind Preventive).</param>
/// <param name="Reminded">Plans whose maintenance staff was reminded.</param>
public record PreventiveMaintenanceRunResult(int Generated, int Reminded);

/// <summary>The maintenance history of a room: its incidents (corrective and preventive) and its preventive plans.</summary>
public record RoomMaintenanceHistory(int RoomId, IReadOnlyList<Incident> Incidents, IReadOnlyList<PreventiveMaintenancePlan> Plans);

public interface IPreventiveMaintenanceCommandService
{
    /// <exception cref="RoomTrack.Shared.Domain.Model.Exceptions.EntityNotFoundException">The room does not exist.</exception>
    Task<PreventiveMaintenancePlan> Handle(SchedulePreventiveMaintenanceCommand command);

    /// <returns>The plan, or null when it does not exist.</returns>
    Task<PreventiveMaintenancePlan?> Handle(ReschedulePreventiveMaintenanceCommand command);

    /// <returns>The plan, or null when it does not exist.</returns>
    Task<PreventiveMaintenancePlan?> Handle(DeactivatePreventiveMaintenanceCommand command);

    Task<PreventiveMaintenanceRunResult> Handle(RunPreventiveMaintenanceCommand command);
}

public interface IPreventiveMaintenanceQueryService
{
    Task<IReadOnlyList<PreventiveMaintenancePlan>> Handle(GetPreventiveMaintenancePlansQuery query);

    Task<PreventiveMaintenancePlan?> Handle(GetPreventiveMaintenancePlanByIdQuery query);

    Task<RoomMaintenanceHistory> Handle(GetRoomMaintenanceHistoryQuery query);
}
