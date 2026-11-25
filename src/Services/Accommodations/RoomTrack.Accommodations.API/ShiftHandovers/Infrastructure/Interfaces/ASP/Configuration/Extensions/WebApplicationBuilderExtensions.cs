using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.ShiftHandovers.Application.Internal.CommandServices;
using RoomTrack.ShiftHandovers.Application.Internal.EventHandlers;
using RoomTrack.ShiftHandovers.Application.Internal.QueryServices;
using RoomTrack.ShiftHandovers.Domain.Model.Events;
using RoomTrack.ShiftHandovers.Domain.Repositories;
using RoomTrack.ShiftHandovers.Domain.Services;
using RoomTrack.ShiftHandovers.Infrastructure.Persistence.EFC;

namespace RoomTrack.ShiftHandovers.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>ShiftHandovers Bounded Context: the summary each shift leaves for the next one (US-32).</summary>
    public static void AddShiftHandoversContextServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IShiftHandoverRepository, ShiftHandoverRepository>();
        builder.Services.AddScoped<IShiftHandoverCommandService, ShiftHandoverCommandService>();
        builder.Services.AddScoped<IShiftHandoverQueryService, ShiftHandoverQueryService>();
        builder.Services.AddScoped<IDomainEventHandler<ShiftHandoverCreatedEvent>, ShiftHandoverPushHandler>();
    }
}
