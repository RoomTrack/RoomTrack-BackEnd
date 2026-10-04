using BackendAwRoomTrack.API.Analytics.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Analytics.Domain.Model.Queries;

namespace BackendAwRoomTrack.API.Analytics.Domain.Services;

/// <summary>
/// Service contract for handling analytics queries.
/// </summary>
public interface IAnalyticsQueryService
{
    Task<PerformanceMetrics> Handle(GetMonthlyPerformanceQuery query);
}