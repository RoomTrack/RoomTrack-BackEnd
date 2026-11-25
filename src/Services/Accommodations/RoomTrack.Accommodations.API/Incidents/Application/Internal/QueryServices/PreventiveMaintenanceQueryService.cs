using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Queries;
using RoomTrack.Incidents.Domain.Repositories;
using RoomTrack.Incidents.Domain.Services;

namespace RoomTrack.Incidents.Application.Internal.QueryServices;

public class PreventiveMaintenanceQueryService(
    IPreventiveMaintenancePlanRepository planRepository,
    IIncidentRepository incidentRepository) : IPreventiveMaintenanceQueryService
{
    public Task<IReadOnlyList<PreventiveMaintenancePlan>> Handle(GetPreventiveMaintenancePlansQuery query) =>
        planRepository.ListAsync(query.HotelId, query.RoomId, query.OnlyActive);

    public Task<PreventiveMaintenancePlan?> Handle(GetPreventiveMaintenancePlanByIdQuery query) =>
        planRepository.FindByIdAsync(query.PlanId);

    public async Task<RoomMaintenanceHistory> Handle(GetRoomMaintenanceHistoryQuery query)
    {
        var incidents = await incidentRepository.ListByRoomAsync(query.RoomId, Math.Clamp(query.Limit, 1, 500));
        var plans = await planRepository.ListAsync(query.HotelId, query.RoomId, onlyActive: false);
        return new RoomMaintenanceHistory(query.RoomId, incidents, plans);
    }
}
