using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomTrack.Accommodations.API.Infrastructure.Persistence;
using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Domain.Model.Entities;
using RoomTrack.Accommodations.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.GuestRequests.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Housekeeping.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Incidents.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Messaging.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Events;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.ShiftHandovers.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.StaffPerformance.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Dashboard.Infrastructure;

namespace RoomTrack.Tests.Features.Support;

/// <summary>
///     The Accommodations service as it is wired in production (the same <c>Add…ContextServices</c> registrations: use
///     cases, repositories, event handlers, push and e-mail notifications) on an in-memory database, with the other
///     services (IAM, Bookings) and the outbound channels (push, e-mail, RabbitMQ) replaced by recording fakes. Each
///     <see cref="Request{TService,TResult}"/> runs in its own DI scope and database context, like an HTTP request.
/// </summary>
/// <remarks>Reqnroll injects one instance per scenario into every binding class that asks for it.</remarks>
public sealed class OperationsWorld : IDisposable
{
    public static readonly DateTimeOffset Start = new(2030, 1, 10, 13, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;

    public OperationsWorld()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.AddAccommodationsContextServices();
        builder.AddIncidentsContextServices();
        builder.AddHousekeepingContextServices();
        builder.AddGuestRequestsContextServices();
        builder.AddMessagingContextServices();
        builder.AddShiftHandoversContextServices();
        builder.AddStaffPerformanceContextServices();
        builder.AddDashboardContextServices();

        var database = Guid.NewGuid().ToString();
        var services = builder.Services;
        services.AddDbContext<AccommodationsDbContext>(options => options.UseInMemoryDatabase(database));
        services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<AccommodationsDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<IPushSender>(Push);
        services.AddSingleton<IEmailSender>(Emails);
        services.AddSingleton<IIamContextFacade>(Iam);
        services.AddSingleton<IRoomReservationsFacade>(Reservations);
        services.AddSingleton(NoOpProxy<IPublishEndpoint>.Create());
        _services = services.BuildServiceProvider();
    }

    public MutableClock Clock { get; } = new(Start);
    public RecordingPush Push { get; } = new();
    public RecordingEmails Emails { get; } = new();
    public StaffDirectory Iam { get; } = new();
    public Stays Reservations { get; } = new();

    public int HotelId { get; private set; }
    public Dictionary<int, int> Rooms { get; } = [];
    public Dictionary<string, int> RoomTypes { get; } = [];

    public void Dispose() => _services.Dispose();

    /// <summary>Runs one request with its own scope (DI and database context).</summary>
    public async Task<TResult> Request<TService, TResult>(Func<TService, Task<TResult>> request) where TService : notnull
    {
        using var scope = _services.CreateScope();
        return await request(scope.ServiceProvider.GetRequiredService<TService>());
    }

    /// <summary>Reads the database as it is now, outside any request.</summary>
    public TResult Read<TResult>(Func<AccommodationsDbContext, TResult> query)
    {
        using var scope = _services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AccommodationsDbContext>());
    }

    /// <summary>A hotel with the rooms <paramref name="numbers"/> of the room type <paramref name="roomType"/>.</summary>
    public void AHotelWithRooms(string roomType, params int[] numbers)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AccommodationsDbContext>();
        if (HotelId == 0)
        {
            var hotel = new Hotel();
            context.Add(hotel);
            context.SaveChanges();
            HotelId = hotel.Id;
        }
        if (!RoomTypes.TryGetValue(roomType, out var roomTypeId))
        {
            var type = new RoomType(roomType, $"{roomType} room");
            context.Add(type);
            context.SaveChanges();
            RoomTypes[roomType] = roomTypeId = type.Id;
        }
        foreach (var number in numbers)
        {
            var room = new Room(new CreateRoomCommand(HotelId, roomTypeId, 120m, $"Room {number}", [], number.ToString()));
            context.Add(room);
            context.SaveChanges();
            Rooms[number] = room.Id;
        }
    }

    public sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public sealed class RecordingPush : IPushSender
    {
        public List<PushMessage> Sent { get; } = [];

        public Task SendAsync(PushMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }

        public IEnumerable<PushMessage> To(int userId) => Sent.Where(message => message.UserIds.Contains(userId));
    }

    public sealed class RecordingEmails : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>The staff of the hotel as the Identity service lists it.</summary>
    public sealed class StaffDirectory : IIamContextFacade
    {
        public List<UserContact> Staff { get; } = [];

        public UserContact Add(int userId, string name, string role)
        {
            var contact = new UserContact(userId, $"{name.ToLowerInvariant()}@roomtrack.pe", name, role);
            Staff.Add(contact);
            return contact;
        }

        public Task<UserContact?> FetchUserContactAsync(int userId) =>
            Task.FromResult(Staff.FirstOrDefault(contact => contact.UserId == userId));

        public Task<IReadOnlyList<UserContact>> ListHotelStaffAsync(int hotelId, IReadOnlyCollection<string> roles) =>
            Task.FromResult<IReadOnlyList<UserContact>>(Staff.Where(contact => roles.Contains(contact.Role)).ToList());

        public Task<int> FetchUserIdByEmail(string email) => throw new NotSupportedException();
        public Task<string> FetchEmailByUserId(int userId) => throw new NotSupportedException();

        public Task<ReissuedSession?> AssignHotelToAdministratorAsync(int userId, int hotelId, SessionContext currentSession) =>
            throw new NotSupportedException();
    }

    /// <summary>The current stays of the guests, as the Bookings service reports them.</summary>
    public sealed class Stays : IRoomReservationsFacade
    {
        public List<GuestStay> Current { get; } = [];

        public Task<IReadOnlyDictionary<int, int>> CountActiveBookingsAsync(IReadOnlyCollection<int> roomIds) =>
            Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());

        public Task<bool> HasCurrentConfirmedStayAsync(int guestUserId, int roomId, DateTime day) =>
            Task.FromResult(Current.Any(stay => stay.GuestUserId == guestUserId && stay.RoomId == roomId));

        public Task<GuestStay?> FetchCurrentStayAsync(int guestUserId) =>
            Task.FromResult(Current.FirstOrDefault(stay => stay.GuestUserId == guestUserId));

        public Task<GuestStay?> FetchCurrentStayOfRoomAsync(int roomId) =>
            Task.FromResult(Current.FirstOrDefault(stay => stay.RoomId == roomId));

        /// <summary>The front desk the Bookings service reports; null makes it unreachable.</summary>
        public FrontDeskSummary? FrontDesk { get; set; } = new(0, 0, 0, 0, null, []);

        public Task<FrontDeskSummary> FetchFrontDeskSummaryAsync(int hotelId) =>
            FrontDesk is { } summary ? Task.FromResult(summary) : throw new HttpRequestException("Bookings is unreachable");
    }

    /// <summary>An implementation of <typeparamref name="T"/> whose every method does nothing (RabbitMQ is not under test).</summary>
    public class NoOpProxy<T> : DispatchProxy where T : class
    {
        public static T Create() => DispatchProxy.Create<T, NoOpProxy<T>>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var returnType = targetMethod?.ReturnType;
            if (returnType == typeof(Task)) return Task.CompletedTask;
            if (returnType is { IsGenericType: true } && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(returnType.GetGenericArguments()[0])
                    .Invoke(null, [returnType.GetGenericArguments()[0].IsValueType ? Activator.CreateInstance(returnType.GetGenericArguments()[0]) : null]);
            return returnType is { IsValueType: true } && returnType != typeof(void) ? Activator.CreateInstance(returnType) : null;
        }
    }
}
