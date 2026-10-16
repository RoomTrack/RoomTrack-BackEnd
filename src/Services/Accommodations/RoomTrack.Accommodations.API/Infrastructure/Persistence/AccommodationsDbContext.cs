using BackendAwRoomTrack.API.Accommodations.Infrastructure.Persistence.EFC.Configuration.Extensions;
using BackendAwRoomTrack.API.Incidents.Infrastructure.Persistence.EFC.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Accommodations.API.Infrastructure.Persistence;

/// <summary>
///     Database of the Accommodations service: hotels, rooms, room types, their catalogs, the room status history and
///     the incidents of the rooms.
/// </summary>
public class AccommodationsDbContext(DbContextOptions<AccommodationsDbContext> options) : AppDbContext(options)
{
    protected override void ApplyServiceConfiguration(ModelBuilder builder)
    {
        builder.ApplyAccommodationsConfiguration();
        builder.ApplyIncidentsConfiguration();
    }
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public class AccommodationsDbContextFactory() : DesignTimeDbContextFactory<AccommodationsDbContext>("roomtrack_accommodations");
