using System;
using System.Linq;
using System.Threading.Tasks;
using BackendAwRoomTrack.API.Accommodations.Application.ACL;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Queries;
using BackendAwRoomTrack.API.Accommodations.Domain.Services;
using BackendAwRoomTrack.API.Accommodations.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Bookings.Interfaces.ACL;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Application.OutboundServices;
using BackendAwRoomTrack.API.Shared.Domain.Repositories;
using BackendAwRoomTrack.API.Shared.Infrastructure.Events;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Repositories;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomTrack.Accommodations.API.Infrastructure.Persistence;
using RoomTrack.BuildingBlocks.Clients;
using Xunit;

namespace BackendAwRoomTrack.API.Tests.Accommodations.Application.ACL;

public class AccommodationsContextFacadeTests
{
    private class FakeHotelQueryService : IHotelQueryService
    {
        public Func<GetHotelByIdQuery, Task<Hotel?>>? GetByIdHandler { get; set; }
        public Func<GetAllHotelsQuery, Task<System.Collections.Generic.IEnumerable<Hotel>>>? GetAllHandler { get; set; }

        public Task<Hotel?> Handle(GetHotelByIdQuery query) =>
            GetByIdHandler != null ? GetByIdHandler(query) : Task.FromResult<Hotel?>(null);

        public Task<System.Collections.Generic.IEnumerable<Hotel>> Handle(GetAllHotelsQuery query) =>
            GetAllHandler != null ? GetAllHandler(query) : Task.FromResult<System.Collections.Generic.IEnumerable<Hotel>>(Enumerable.Empty<Hotel>());
    }

    // HotelExistsAsync only uses the hotel query service; the other collaborators are not needed.
    private static AccommodationsContextFacade CreateFacade(IHotelQueryService hotelQueryService) =>
        new(hotelQueryService, null!, null!, null!, null!);

    [Fact]
    public async Task HotelExistsAsync_WhenHotelExists_ShouldReturnTrue()
    {
        // Arrange
        const int validHotelId = 101;
        var fakeQueryService = new FakeHotelQueryService
        {
            GetByIdHandler = q => Task.FromResult<Hotel?>(q.HotelId == validHotelId ? new Hotel() : null)
        };
        var facade = CreateFacade(fakeQueryService);

        // Act
        var result = await facade.HotelExistsAsync(validHotelId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HotelExistsAsync_WhenHotelDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const int nonExistentHotelId = 999999;
        var fakeQueryService = new FakeHotelQueryService
        {
            GetByIdHandler = _ => Task.FromResult<Hotel?>(null)
        };
        var facade = CreateFacade(fakeQueryService);

        // Act
        var result = await facade.HotelExistsAsync(nonExistentHotelId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HotelExistsAsync_WhenHotelIdIsZeroOrNegative_ShouldReturnFalse()
    {
        // Arrange
        var fakeQueryService = new FakeHotelQueryService();
        var facade = CreateFacade(fakeQueryService);

        // Act
        var resultZero = await facade.HotelExistsAsync(0);
        var resultNegative = await facade.HotelExistsAsync(-5);

        // Assert
        resultZero.Should().BeFalse();
        resultNegative.Should().BeFalse();
    }

    [Fact]
    public async Task AddAccommodationsContextServices_ShouldRegisterIAccommodationsContextFacade()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        // The service's context (exposed as AppDbContext) with InMemory, and the shared persistence services
        builder.Services.AddDbContext<AccommodationsDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddScoped<AppDbContext>(provider => provider.GetRequiredService<AccommodationsDbContext>());
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        builder.Services.AddSingleton(TimeProvider.System);
        // Integration events: in-memory bus (never started) instead of RabbitMQ
        builder.Services.AddMassTransit(bus => bus.UsingInMemory());

        // Ports to the other services (HTTP clients, as in Program.cs)
        builder.Services.AddHttpClient<IIamContextFacade, HttpIamContextFacade>(client =>
            client.BaseAddress = new Uri("http://identity.test/"));
        builder.Services.AddHttpClient<IRoomReservationsFacade, HttpRoomReservationsFacade>(client =>
            client.BaseAddress = new Uri("http://bookings.test/"));

        // Act
        builder.AddAccommodationsContextServices();
        await using var serviceProvider = builder.Services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        // Assert
        var facade = scope.ServiceProvider.GetService<IAccommodationsContextFacade>();
        facade.Should().NotBeNull();
        facade.Should().BeOfType<AccommodationsContextFacade>();
    }

    [Fact]
    public void IAccommodationsContextFacade_Contract_ShouldNotExposeInternalDomainEntities()
    {
        // Arrange & Act
        var methods = typeof(IAccommodationsContextFacade).GetMethods();

        // Assert: Methods in IAccommodationsContextFacade must only return primitive/value types or DTOs
        foreach (var method in methods)
        {
            foreach (var type in Flatten(method.ReturnType))
            {
                type.Namespace.Should().NotContain(".Domain.Model", "Internal domain entities must not be leaked across ACL");
                typeof(DbContext).IsAssignableFrom(type).Should().BeFalse("EF Core context must not be leaked across ACL");
            }
        }
    }

    // The type and, recursively, its generic arguments (Task<IReadOnlyList<RoomOffer>> -> Task, IReadOnlyList, RoomOffer).
    private static System.Collections.Generic.IEnumerable<Type> Flatten(Type type) =>
        new[] { type }.Concat(type.GetGenericArguments().SelectMany(Flatten));
}
