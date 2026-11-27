using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.StaffPerformance.Domain.Model.Queries;
using RoomTrack.StaffPerformance.Domain.Model.ValueObjects;
using RoomTrack.StaffPerformance.Domain.Repositories;
using RoomTrack.StaffPerformance.Domain.Services;

namespace RoomTrack.StaffPerformance.Application.Internal.QueryServices;

public class StaffPerformanceQueryService(
    IFinishedWorkRepository finishedWorkRepository,
    IIamContextFacade iamContextFacade,
    ILogger<StaffPerformanceQueryService> logger) : IStaffPerformanceQueryService
{
    private static readonly string[] StaffRoles =
    [
        UserRoles.Reception, UserRoles.Housekeeping, UserRoles.Maintenance, UserRoles.Admin, UserRoles.ChainAdmin
    ];

    public async Task<ProductivityReport> Handle(GetStaffProductivityQuery query)
    {
        var work = await finishedWorkRepository.ListFinishedAsync(query.HotelId, query.From, query.To);
        var staff = await StaffOfAsync(query.HotelId);
        return StaffPerformanceCalculator.Productivity(query.HotelId, query.From, query.To, work, staff);
    }

    public async Task<ServiceTimesReport> Handle(GetServiceTimesQuery query)
    {
        var work = await finishedWorkRepository.ListFinishedAsync(query.HotelId, query.From, query.To);
        return StaffPerformanceCalculator.ServiceTimes(query.HotelId, query.From, query.To, work);
    }

    /// <summary>
    ///     The names of the staff of the hotel. The report is still useful without them (each row keeps its user id), so
    ///     an unavailable Identity service does not fail it.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, StaffMember>> StaffOfAsync(int hotelId)
    {
        try
        {
            var contacts = await iamContextFacade.ListHotelStaffAsync(hotelId, StaffRoles);
            return contacts
                .GroupBy(contact => contact.UserId)
                .ToDictionary(group => group.Key,
                    group => new StaffMember(group.Key, group.First().FullName, group.First().Email, group.First().Role));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not read the staff of hotel {HotelId}; the productivity report goes without names",
                hotelId);
            return new Dictionary<int, StaffMember>();
        }
    }
}
