using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;

/// <summary>
///     Design-time factory for <c>dotnet ef</c> (migrations are created without a running database). Each service
///     derives one for its context, naming its local development database.
/// </summary>
/// <typeparam name="TContext">The service's context; it must have a constructor taking <see cref="DbContextOptions{TContext}"/>.</typeparam>
public abstract class DesignTimeDbContextFactory<TContext>(string localDatabase) : IDesignTimeDbContextFactory<TContext>
    where TContext : AppDbContext
{
    public TContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        // Design-time only (dotnet ef). Uses the same env var as the service; falls back to the local docker compose DB.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = $"server=localhost;port=3306;user=roomtrack;password=roomtrack_dev;database={localDatabase};";

        optionsBuilder.UseMySql(connectionString, serverVersion);

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }
}
