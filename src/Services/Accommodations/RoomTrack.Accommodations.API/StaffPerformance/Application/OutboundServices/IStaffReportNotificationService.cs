using RoomTrack.StaffPerformance.Domain.Model.Aggregates;
using RoomTrack.StaffPerformance.Domain.Model.ValueObjects;

namespace RoomTrack.StaffPerformance.Application.OutboundServices;

/// <summary>The staff reports of a period, ready to be delivered (only the ones the schedule includes).</summary>
public sealed record StaffReports(
    string HotelName,
    ReportFrequency Frequency,
    DateTimeOffset From,
    DateTimeOffset To,
    ProductivityReport? Productivity,
    ServiceTimesReport? ServiceTimes);

/// <summary>Delivers the staff reports to the recipients of a schedule (US-34).</summary>
public interface IStaffReportNotificationService
{
    Task SendAsync(IReadOnlyList<string> recipients, StaffReports reports);
}
