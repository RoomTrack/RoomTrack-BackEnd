using RoomTrack.StaffPerformance.Domain.Model.Queries;
using RoomTrack.StaffPerformance.Domain.Model.ValueObjects;

namespace RoomTrack.StaffPerformance.Domain.Services;

public interface IStaffPerformanceQueryService
{
    Task<ProductivityReport> Handle(GetStaffProductivityQuery query);

    Task<ServiceTimesReport> Handle(GetServiceTimesQuery query);
}
