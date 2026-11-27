using RoomTrack.ShiftHandovers.Infrastructure.Persistence.EFC;
using RoomTrack.StaffPerformance.Infrastructure.Persistence.EFC;
using RoomTrack.Messaging.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.GuestRequests.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Accommodations.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Housekeeping.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Incidents.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Accommodations.API.Infrastructure.Persistence;

/// <summary>
///     Database of the Accommodations service: hotels, rooms, room types, their catalogs, the room status history, the
///     incidents and the cleaning tasks of the rooms, the requests of the guests and the conversations.
/// </summary>
public class AccommodationsDbContext(DbContextOptions<AccommodationsDbContext> options) : AppDbContext(options)
{
    protected override void ApplyServiceConfiguration(ModelBuilder builder)
    {
        builder.ApplyAccommodationsConfiguration();
        builder.ApplyIncidentsConfiguration();
        builder.ApplyPreventiveMaintenanceConfiguration();
        builder.ApplyHousekeepingConfiguration();
        builder.ApplyGuestRequestsConfiguration();
        builder.ApplyMessagingConfiguration();
        builder.ApplyShiftHandoversConfiguration();
        builder.ApplyStaffPerformanceConfiguration();
    }
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public class AccommodationsDbContextFactory() : DesignTimeDbContextFactory<AccommodationsDbContext>("roomtrack_accommodations");
