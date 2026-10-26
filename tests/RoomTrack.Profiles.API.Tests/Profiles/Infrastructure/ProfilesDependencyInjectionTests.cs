using RoomTrack.Accommodations.Interfaces.ACL;
using RoomTrack.Profiles.Application.Internal.CommandServices;
using RoomTrack.Profiles.Application.Internal.OutboundServices;
using RoomTrack.Profiles.Application.Internal.QueryServices;
using RoomTrack.Profiles.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Profiles.Interfaces.ACL;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Events;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Shared.Infrastructure.Mediator.Cortex.Configuration.Extensions;
using RoomTrack.Profiles.Domain.Repositories;
using RoomTrack.Profiles.Domain.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomTrack.BuildingBlocks.Clients;
using RoomTrack.Profiles.API.Infrastructure.Persistence;
using Xunit;

namespace RoomTrack.Tests.Profiles.Infrastructure;

public class ProfilesDependencyInjectionTests
{
    [Fact]
    public void AddProfilesContextServices_ShouldRegisterAllRequiredProfilesServices()
    {
        var builder = WebApplication.CreateBuilder();

        // Register the service's context (exposed as AppDbContext) with InMemory for test DI resolution
        builder.Services.AddDbContext<ProfilesDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddScoped<AppDbContext>(provider => provider.GetRequiredService<ProfilesDbContext>());

        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        builder.AddCortexMediatorServices<Program>();

        // Register the Accommodations port (HTTP client of the Accommodations service) & Profiles services
        builder.Services.AddHttpClient<IAccommodationsContextFacade, HttpAccommodationsContextFacade>(client =>
            client.BaseAddress = new Uri("http://accommodations.test/"));
        builder.AddProfilesContextServices();

        var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;

        // Repositories
        sp.GetService<IGuestProfileRepository>().Should().NotBeNull();
        sp.GetService<IStaffProfileRepository>().Should().NotBeNull();

        // Domain / Outbound Services
        sp.GetService<IEmployeeCodeGenerator>().Should().NotBeNull();
        sp.GetService<IDomainEventPublisher>().Should().NotBeNull();

        // Command Services
        sp.GetService<IGuestProfileCommandService>().Should().NotBeNull();
        sp.GetService<IStaffProfileCommandService>().Should().NotBeNull();

        // Query Services
        sp.GetService<IGuestProfileQueryService>().Should().NotBeNull();
        sp.GetService<IStaffProfileQueryService>().Should().NotBeNull();

        // ACL Facades
        sp.GetService<IGuestProfilesContextFacade>().Should().NotBeNull();
        sp.GetService<IStaffProfilesContextFacade>().Should().NotBeNull();
    }
}
