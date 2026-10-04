using BackendAwRoomTrack.Domain.Shared.Domain.Model.Events;

namespace BackendAwRoomTrack.API.Profiles.Application.Internal.OutboundServices;

public interface IDomainEventPublisher
{
    Task PublishAsync(IReadOnlyCollection<IEvent> domainEvents, CancellationToken cancellationToken = default);
}
