using BackendAwRoomTrack.API.Audit.Infrastructure.Persistence.EFC.Configuration.Extensions;
using BackendAwRoomTrack.API.IAM.Infrastructure.Persistence.EFC.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Identity.API.Infrastructure.Persistence;

/// <summary>Database of the Identity service: user accounts, sessions and MFA (IAM) and the access audit log.</summary>
public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : AppDbContext(options)
{
    protected override void ApplyServiceConfiguration(ModelBuilder builder)
    {
        builder.ApplyIamConfiguration();
        builder.ApplyAuditConfiguration();
    }
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public class IdentityDbContextFactory() : DesignTimeDbContextFactory<IdentityDbContext>("roomtrack_identity");
