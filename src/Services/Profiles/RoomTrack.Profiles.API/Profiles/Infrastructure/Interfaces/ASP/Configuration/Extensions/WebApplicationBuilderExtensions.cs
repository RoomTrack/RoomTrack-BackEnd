using RoomTrack.Profiles.Application.ACL;
using RoomTrack.Profiles.Application.Internal.CommandServices;
using RoomTrack.Profiles.Application.Internal.OutboundServices;
using RoomTrack.Profiles.Application.Internal.QueryServices;
using RoomTrack.Profiles.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Profiles.Interfaces.ACL;
using RoomTrack.Profiles.Domain.Repositories;
using RoomTrack.Profiles.Domain.Services;

namespace RoomTrack.Profiles.Infrastructure.Interfaces.ASP.Configuration.Extensions;

/// <summary>
/// Provides extension methods for configuring Profile context services in the application.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Registers all Profile-related services into the dependency injection container.
    /// </summary>
    public static void AddProfilesContextServices(this WebApplicationBuilder builder)
    {
        // Repositories
        builder.Services.AddScoped<IGuestProfileRepository, GuestProfileRepository>();
        builder.Services.AddScoped<IStaffProfileRepository, StaffProfileRepository>();

        // Domain & Outbound Services
        builder.Services.AddScoped<IEmployeeCodeGenerator, EmployeeCodeGenerator>();
        builder.Services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();

        // Guest Application Services
        builder.Services.AddScoped<IGuestProfileCommandService, GuestProfileCommandService>();
        builder.Services.AddScoped<IGuestProfileQueryService, GuestProfileQueryService>();

        // Staff Application Services
        builder.Services.AddScoped<IStaffProfileCommandService, StaffProfileCommandService>();
        builder.Services.AddScoped<IStaffProfileQueryService, StaffProfileQueryService>();

        // ACL Facades
        builder.Services.AddScoped<IGuestProfilesContextFacade, GuestProfilesContextFacade>();
        builder.Services.AddScoped<IStaffProfilesContextFacade, StaffProfilesContextFacade>();

        // Resource-based authorization (a guest acts only on their own profile)
        builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
            RoomTrack.Profiles.Interfaces.REST.Authorization.GuestAccountAuthorizationHandler>();
    }
}