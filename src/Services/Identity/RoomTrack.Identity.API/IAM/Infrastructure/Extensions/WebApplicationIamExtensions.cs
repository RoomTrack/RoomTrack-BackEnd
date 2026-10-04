using BackendAwRoomTrack.API.IAM.Infrastructure.Seed;

namespace BackendAwRoomTrack.API.IAM.Infrastructure.Extensions;

public static class WebApplicationIamExtensions
{
    /// <summary>
    /// Seeds initial IAM data (chain admin user) if configured. Errors propagate: startup fails fast.
    /// </summary>
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        await IamSeeder.SeedAsync(scope.ServiceProvider);
    }
}
