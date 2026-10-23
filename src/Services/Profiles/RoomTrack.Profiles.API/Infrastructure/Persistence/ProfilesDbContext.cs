using BackendAwRoomTrack.API.Marketing.Infrastructure.Persistence.EFC.Configuration.Extensions;
using BackendAwRoomTrack.API.Profiles.Infrastructure.Persistence.EFC.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Profiles.API.Infrastructure.Persistence;

/// <summary>Database of the Profiles service: guest and staff profiles (Profiles) and the landing's demo requests (Marketing).</summary>
public class ProfilesDbContext(DbContextOptions<ProfilesDbContext> options) : AppDbContext(options)
{
    protected override void ApplyServiceConfiguration(ModelBuilder builder)
    {
        builder.ApplyProfilesConfiguration();
        builder.ApplyMarketingConfiguration();
    }
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public class ProfilesDbContextFactory() : DesignTimeDbContextFactory<ProfilesDbContext>("roomtrack_profiles");
