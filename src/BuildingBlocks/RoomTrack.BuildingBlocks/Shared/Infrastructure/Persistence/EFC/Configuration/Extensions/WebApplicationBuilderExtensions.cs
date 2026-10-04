using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace BackendAwRoomTrack.API.shared.Infrastructure.Persistence.EFC.Configuration.Extensions;

/// <summary>
/// Provides extension methods for <see cref="WebApplicationBuilder"/> to configure persistence-related services.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    ///     MySQL server version assumed when <c>Database:MySqlServerVersion</c> is not configured.
    ///     Pinning the version avoids <c>ServerVersion.AutoDetect</c>, which opens a DB connection
    ///     every time the context options are built and fails when the DB is not reachable yet.
    /// </summary>
    private const string DefaultMySqlServerVersion = "8.0.36";

    /// <summary>
    ///     Registers <typeparamref name="TContext"/> (the service's own database) and exposes it as
    ///     <see cref="AppDbContext"/> to the shared repositories and unit of work.
    /// </summary>
    public static void AddDatabaseConfigurationServices<TContext>(this WebApplicationBuilder builder)
        where TContext : AppDbContext
    {
        // Fail fast: resolve and validate the connection string once, at startup.
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. " +
                "Set the 'ConnectionStrings__DefaultConnection' environment variable " +
                "(e.g. server=host;port=3306;user=...;password=...;database=...;).");
        }

        var serverVersion = MySqlServerVersionFrom(builder.Configuration);

        builder.Services.AddDbContext<TContext>(options =>
        {
            if (builder.Environment.IsDevelopment())
            {
                options.UseMySql(connectionString, serverVersion)
                    .LogTo(Console.WriteLine, LogLevel.Information)
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            }
            else
            {
                options.UseMySql(connectionString, serverVersion)
                    .LogTo(Console.WriteLine, LogLevel.Error)
                    .EnableDetailedErrors();
            }
        });
        builder.Services.AddScoped<AppDbContext>(provider => provider.GetRequiredService<TContext>());

        builder.Services.AddHealthChecks()
            .AddMySql(connectionString, name: "mysql-db-check", tags: ["database"]);
    }

    /// <summary>The configured MySQL server version (<c>Database:MySqlServerVersion</c>), 8.0.36 by default.</summary>
    public static MySqlServerVersion MySqlServerVersionFrom(IConfiguration configuration)
    {
        var versionText = configuration["Database:MySqlServerVersion"];
        return new MySqlServerVersion(
            Version.Parse(string.IsNullOrWhiteSpace(versionText) ? DefaultMySqlServerVersion : versionText));
    }

    /// <summary>
    ///     Applies the pending migrations of <typeparamref name="TContext"/>. Fail fast: the service never starts with
    ///     a broken schema.
    /// </summary>
    public static async Task MigrateDatabaseAsync<TContext>(this WebApplication app) where TContext : AppDbContext
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();
        try
        {
            if (!context.Database.IsRelational()) return;
            logger.LogInformation("Applying pending database migrations of {Context}...", typeof(TContext).Name);
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database migration failed at startup. The service will stop.");
            throw;
        }
    }
}
