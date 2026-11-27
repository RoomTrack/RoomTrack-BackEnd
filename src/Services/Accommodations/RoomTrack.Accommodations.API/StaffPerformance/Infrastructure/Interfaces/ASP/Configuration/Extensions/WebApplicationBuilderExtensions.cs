using RoomTrack.StaffPerformance.Application.Internal.CommandServices;
using RoomTrack.StaffPerformance.Application.Internal.QueryServices;
using RoomTrack.StaffPerformance.Application.OutboundServices;
using RoomTrack.StaffPerformance.Infrastructure.Notifications;
using RoomTrack.StaffPerformance.Domain.Repositories;
using RoomTrack.StaffPerformance.Domain.Services;
using RoomTrack.StaffPerformance.Infrastructure.Persistence.EFC;

namespace RoomTrack.StaffPerformance.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    ///     StaffPerformance Bounded Context: staff productivity (US-24) and service times (US-25), read from the work
    ///     the Housekeeping, Incidents and GuestRequests contexts record, and their export and scheduled delivery by
    ///     e-mail (US-34).
    /// </summary>
    public static void AddStaffPerformanceContextServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IFinishedWorkRepository, FinishedWorkRepository>();
        builder.Services.AddScoped<IStaffPerformanceQueryService, StaffPerformanceQueryService>();

        // Scheduled delivery of the reports by e-mail (US-34)
        builder.Services.AddScoped<IReportScheduleRepository, ReportScheduleRepository>();
        builder.Services.AddScoped<IReportScheduleCommandService, ReportScheduleCommandService>();
        builder.Services.AddScoped<IReportScheduleQueryService, ReportScheduleQueryService>();
        builder.Services.AddScoped<IStaffReportNotificationService, StaffReportEmailNotificationService>();
    }
}
