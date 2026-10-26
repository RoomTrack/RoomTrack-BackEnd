using RoomTrack.Shared.Domain.Model.Events;

namespace RoomTrack.Profiles.Application.Internal.OutboundServices;

public interface IDomainEventPublisher
{
    Task PublishAsync(IReadOnlyCollection<IEvent> domainEvents, CancellationToken cancellationToken = default);
}
