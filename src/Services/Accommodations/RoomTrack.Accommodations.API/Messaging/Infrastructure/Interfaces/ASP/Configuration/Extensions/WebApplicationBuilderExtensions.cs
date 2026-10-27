using RoomTrack.Messaging.Application.Internal.CommandServices;
using RoomTrack.Messaging.Application.Internal.EventHandlers;
using RoomTrack.Messaging.Application.Internal.QueryServices;
using RoomTrack.Messaging.Domain.Model.Events;
using RoomTrack.Messaging.Domain.Repositories;
using RoomTrack.Messaging.Domain.Services;
using RoomTrack.Messaging.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Shared.Application.Internal.EventHandlers;

namespace RoomTrack.Messaging.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>Messaging context: guests with the front desk (US-20), area channels and task comments (US-12).</summary>
    public static void AddMessagingContextServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
        builder.Services.AddScoped<IConversationMessageRepository, ConversationMessageRepository>();
        builder.Services.AddScoped<IMessagingCommandService, MessagingCommandService>();
        builder.Services.AddScoped<IMessagingQueryService, MessagingQueryService>();

        // Push notification of every new message to the other side (US-20 scenario 2, US-22)
        builder.Services.AddScoped<IDomainEventHandler<MessagePostedEvent>, MessagePushHandler>();
    }
}
