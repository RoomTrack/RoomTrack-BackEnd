using RoomTrack.Dashboard.Application;

namespace RoomTrack.Dashboard.Infrastructure;

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    ///     Dashboard context: the administrator's panel of a hotel (US-18), read from the Accommodations, Housekeeping,
    ///     Incidents and GuestRequests contexts and from the front desk of the Bookings service.
    /// </summary>
    public static void AddDashboardContextServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IHotelOperationsRepository, HotelOperationsRepository>();
        builder.Services.AddScoped<IAdminDashboardQueryService, AdminDashboardQueryService>();
    }
}
