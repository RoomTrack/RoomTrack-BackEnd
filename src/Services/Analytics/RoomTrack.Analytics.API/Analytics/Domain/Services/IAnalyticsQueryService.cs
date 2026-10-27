using RoomTrack.Analytics.Domain.Model.Aggregates;
using RoomTrack.Analytics.Domain.Model.Queries;

namespace RoomTrack.Analytics.Domain.Services;

/// <summary>
/// Service contract for handling analytics queries.
/// </summary>
public interface IAnalyticsQueryService
{
    Task<PerformanceMetrics> Handle(GetMonthlyPerformanceQuery query);
}