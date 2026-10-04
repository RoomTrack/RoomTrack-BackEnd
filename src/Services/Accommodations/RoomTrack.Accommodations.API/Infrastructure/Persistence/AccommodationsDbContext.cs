using BackendAwRoomTrack.API.Accommodations.Infrastructure.Persistence.EFC.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Accommodations.API.Infrastructure.Persistence;

/// <summary>Database of the Accommodations service: hotels, rooms, room types, their catalogs and the room status history.</summary>
public class AccommodationsDbContext(DbContextOptions<AccommodationsDbContext> options) : AppDbContext(options)
{
    protected override void ApplyServiceConfiguration(ModelBuilder builder) => builder.ApplyAccommodationsConfiguration();
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public class AccommodationsDbContextFactory() : DesignTimeDbContextFactory<AccommodationsDbContext>("roomtrack_accommodations");
