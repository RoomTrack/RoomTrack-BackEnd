using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Shared.Domain.Model;
using EntityFrameworkCore.CreatedUpdatedDate.Extensions;
using MassTransit;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;

/// <summary>
///     Base database context of every service. Each service owns its database and derives its own context from this
///     one, applying only the configuration of its bounded contexts in <see cref="ApplyServiceConfiguration"/>.
/// </summary>
/// <remarks>
///     Repositories and the unit of work depend on this base type, so they are reused unchanged by every service
///     (the concrete context is registered as <see cref="AppDbContext"/> too). The context also holds the
///     Data Protection key ring and the MassTransit transactional outbox: messages published while handling a
///     request (integration events, e-mails) are stored by the same <c>SaveChanges</c> as the business change and
///     relayed to RabbitMQ afterwards, so a message exists if and only if its change committed.
/// </remarks>
public abstract class AppDbContext(DbContextOptions options) : DbContext(options), IDataProtectionKeyContext
{
    /// <summary>Key ring of ASP.NET Core Data Protection (persisted so it survives container restarts).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder builder)
    {
        builder.AddCreatedUpdatedInterceptor();
        base.OnConfiguring(builder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        IncrementConcurrencyVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        IncrementConcurrencyVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    ///     The version is a concurrency token: the UPDATE only matches the row while it still has the version that was
    ///     read (<see cref="DbUpdateConcurrencyException"/> otherwise), and it stores the next one.
    /// </summary>
    private void IncrementConcurrencyVersions()
    {
        foreach (var entry in ChangeTracker.Entries<IHasConcurrencyVersion>().Where(entry => entry.State == EntityState.Modified))
            entry.Property(aggregate => aggregate.Version).CurrentValue = entry.Entity.Version + 1;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ApplyServiceConfiguration(builder);

        // Transactional outbox (and inbox for idempotent consumers) of MassTransit
        builder.AddInboxStateEntity();
        builder.AddOutboxMessageEntity();
        builder.AddOutboxStateEntity();

        // Apply snake_case naming convention for database compatibility (e.g., MySQL)
        builder.UseSnakeCaseNamingConvention();
    }

    /// <summary>Applies the model configuration of the bounded contexts owned by the service.</summary>
    protected abstract void ApplyServiceConfiguration(ModelBuilder builder);
}
