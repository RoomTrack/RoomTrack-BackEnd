using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Dashboard.Domain.Model;
using RoomTrack.Dashboard.Domain.Services;
using RoomTrack.Dashboard.Infrastructure;

namespace RoomTrack.Dashboard.Application;

/// <summary>US-18: the administrator's panel of a hotel, with what overdue means for its cleaning tasks.</summary>
public record GetAdminDashboardQuery(int HotelId, TimeSpan CleaningOverdueAfter);

public interface IAdminDashboardQueryService
{
    Task<AdminDashboard> Handle(GetAdminDashboardQuery query);
}

/// <summary>
///     Builds the panel from the operational state of the hotel (this service) and the front desk of today (the Bookings
///     service). Without Bookings the panel still answers, without the front desk.
/// </summary>
public class AdminDashboardQueryService(
    IHotelOperationsRepository operationsRepository,
    IRoomReservationsFacade roomReservationsFacade,
    TimeProvider timeProvider,
    ILogger<AdminDashboardQueryService> logger) : IAdminDashboardQueryService
{
    public async Task<AdminDashboard> Handle(GetAdminDashboardQuery query)
    {
        var operations = await operationsRepository.ReadAsync(query.HotelId);
        FrontDeskSummary? frontDesk;
        try
        {
            frontDesk = await roomReservationsFacade.FetchFrontDeskSummaryAsync(query.HotelId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not read the front desk of hotel {HotelId}; the panel goes without it", query.HotelId);
            frontDesk = null;
        }
        return AdminDashboardCalculator.Build(query.HotelId, operations, frontDesk, query.CleaningOverdueAfter,
            timeProvider.GetUtcNow());
    }
}
