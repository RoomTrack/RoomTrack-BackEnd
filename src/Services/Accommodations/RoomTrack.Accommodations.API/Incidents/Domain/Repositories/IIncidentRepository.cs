using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Queries;

namespace RoomTrack.Incidents.Domain.Repositories;

public interface IIncidentRepository
{
    Task AddAsync(Incident incident);

    /// <summary>The incident with its history, or null.</summary>
    Task<Incident?> FindByIdAsync(int id);

    /// <summary>The incidents matching the query, most urgent first (without their history).</summary>
    Task<IReadOnlyList<Incident>> ListAsync(GetIncidentsQuery query);

    /// <summary>Another open incident (not <paramref name="excludingIncidentId"/>) still blocks the room.</summary>
    Task<bool> AnyOpenBlockingAsync(int roomId, int excludingIncidentId);

    /// <summary>Open critical incidents not started nor escalated yet (with their history).</summary>
    Task<IReadOnlyList<Incident>> ListEscalationCandidatesAsync();

    /// <summary>
    ///     US-10: incidents of the room with the same title (case-insensitive) reported since <paramref name="since"/>,
    ///     counting the ones added in this unit of work.
    /// </summary>
    Task<int> CountSimilarSinceAsync(int roomId, string title, DateTimeOffset since);

    /// <summary>Incidents of the hotel escalated or flagged as recurring in the period (US-23), with their history.</summary>
    Task<IReadOnlyList<Incident>> ListAlertedAsync(int hotelId, DateTimeOffset from, DateTimeOffset to, int limit);

    /// <summary>Every incident of the room, corrective and preventive, newest first (US-10, US-30).</summary>
    Task<IReadOnlyList<Incident>> ListByRoomAsync(int roomId, int limit);
}

public interface IPreventiveMaintenancePlanRepository
{
    Task AddAsync(PreventiveMaintenancePlan plan);

    Task<PreventiveMaintenancePlan?> FindByIdAsync(int id);

    /// <summary>The plans of a hotel (optionally of one room), next due first.</summary>
    Task<IReadOnlyList<PreventiveMaintenancePlan>> ListAsync(int hotelId, int? roomId, bool onlyActive);

    /// <summary>Active plans whose task is due by <paramref name="today"/> or whose reminder window started.</summary>
    Task<IReadOnlyList<PreventiveMaintenancePlan>> ListActionableAsync(DateOnly today, int maxReminderDays);
}
