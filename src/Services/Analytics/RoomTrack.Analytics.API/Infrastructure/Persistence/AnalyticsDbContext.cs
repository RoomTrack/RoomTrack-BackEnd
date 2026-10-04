using BackendAwRoomTrack.API.Analytics.Infrastructure.Persistence.EFC.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Analytics.API.Infrastructure.Persistence;

/// <summary>Database of the Analytics service: its read model of rooms, bookings and payments.</summary>
public class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : AppDbContext(options)
{
    protected override void ApplyServiceConfiguration(ModelBuilder builder) => builder.ApplyAnalyticsConfiguration();
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public class AnalyticsDbContextFactory() : DesignTimeDbContextFactory<AnalyticsDbContext>("roomtrack_analytics");
