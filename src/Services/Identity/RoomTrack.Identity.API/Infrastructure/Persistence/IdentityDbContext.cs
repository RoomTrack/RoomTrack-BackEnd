using RoomTrack.Notifications.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Audit.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.IAM.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Identity.API.Infrastructure.Persistence;

/// <summary>Database of the Identity service: user accounts, sessions and MFA (IAM), the access audit log and the push
///     notification devices and preferences.</summary>
public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : AppDbContext(options)
{
    protected override void ApplyServiceConfiguration(ModelBuilder builder)
    {
        builder.ApplyIamConfiguration();
        builder.ApplyAuditConfiguration();
        builder.ApplyNotificationsConfiguration();
    }
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public class IdentityDbContextFactory() : DesignTimeDbContextFactory<IdentityDbContext>("roomtrack_identity");
