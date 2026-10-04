using BackendAwRoomTrack.API.Profiles.Application.ACL;
using BackendAwRoomTrack.API.Profiles.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Profiles.Application.Internal.OutboundServices;
using BackendAwRoomTrack.API.Profiles.Application.Internal.QueryServices;
using BackendAwRoomTrack.API.Profiles.Infrastructure.Persistence.EFC.Repositories;
using BackendAwRoomTrack.API.Profiles.Interfaces.ACL;
using BackendAwRoomTrack.Domain.Profiles.Domain.Repositories;
using BackendAwRoomTrack.Domain.Profiles.Domain.Services;

namespace BackendAwRoomTrack.API.Profiles.Infrastructure.Interfaces.ASP.Configuration.Extensions;

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
            BackendAwRoomTrack.API.Profiles.Interfaces.REST.Authorization.GuestAccountAuthorizationHandler>();
    }
}