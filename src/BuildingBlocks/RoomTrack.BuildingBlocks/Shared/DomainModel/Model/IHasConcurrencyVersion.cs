namespace RoomTrack.Shared.Domain.Model;

/// <summary>
///     An aggregate protected against lost updates (optimistic concurrency). The database context increments
///     <see cref="Version"/> every time it saves a change of the aggregate and only saves it when the stored version is
///     still the one that was read: when two requests change the same aggregate at once, the second one fails with a
///     409 instead of silently overwriting the first.
/// </summary>
public interface IHasConcurrencyVersion
{
    int Version { get; }
}
