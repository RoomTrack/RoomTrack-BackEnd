using RoomTrack.Shared.Domain.Model.Events;
using Cortex.Mediator.Notifications;

namespace RoomTrack.Shared.Infrastructure.Mediator;

/// <summary>
/// Infrastructure adapter that wraps a pure domain event as a Cortex INotification
/// so Cortex Mediator can dispatch it to handlers without contaminating the Domain project.
/// </summary>
public record DomainEventNotificationAdapter<TEvent>(TEvent Event) : INotification where TEvent : IEvent;
