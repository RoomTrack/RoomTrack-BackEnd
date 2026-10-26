using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.GuestRequests.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Housekeeping.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Incidents.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Messaging.Application.Internal.CommandServices;
using RoomTrack.Messaging.Application.Internal.EventHandlers;
using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Messaging.Domain.Model.Commands;
using RoomTrack.Messaging.Domain.Model.Events;
using RoomTrack.Messaging.Domain.Model.Exceptions;
using RoomTrack.Messaging.Domain.Model.ValueObjects;
using RoomTrack.Messaging.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Shared.Domain.Model.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RoomTrack.Accommodations.API.Infrastructure.Persistence;
using RoomTrack.Contracts.Messaging;
using Xunit;

namespace RoomTrack.Tests.Messaging;

/// <summary>Conversations of a hotel (US-12, US-20) against an in-memory database, with their push notifications.</summary>
public class MessagingTests : IDisposable
{
    private const int HotelId = 1;
    private static readonly ConversationActor Guest = new(50, "Lucía", UserRoles.Guest, null);
    private static readonly ConversationActor Reception = new(10, "Recepción", UserRoles.Reception, HotelId);
    private static readonly ConversationActor Housekeeper = new(30, "Ana", UserRoles.Housekeeping, HotelId);
    private static readonly ConversationActor OtherHousekeeper = new(31, "Beto", UserRoles.Housekeeping, HotelId);
    private static readonly ConversationActor Maintenance = new(20, "Mario", UserRoles.Maintenance, HotelId);
    private static readonly ConversationActor OtherHotelReception = new(11, "Otra", UserRoles.Reception, HotelId + 1);

    private readonly AccommodationsDbContext _context = new(new DbContextOptionsBuilder<AccommodationsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly FakeReservations _reservations = new();
    private readonly RecordingPushSender _push = new();
    private readonly MessagingCommandService _service;

    public MessagingTests()
    {
        var conversations = new ConversationRepository(_context);
        var messages = new ConversationMessageRepository(_context);
        var tasks = new CleaningTaskRepository(_context);
        var incidents = new IncidentRepository(_context);
        var requests = new GuestRequestRepository(_context);
        var pushHandler = new MessagePushHandler(new FakeIam(), messages, tasks, incidents, requests, _push);
        _service = new MessagingCommandService(conversations, messages, _reservations, tasks, incidents, requests,
            new UnitOfWork(_context, new Dispatcher(pushHandler)), TimeProvider.System);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task AGuest_ShouldWriteToTheFrontDeskOfTheirStay_InOneConversation_AndReceptionIsNotified()
    {
        _reservations.Stay = new GuestStay(7, "SS-ABCDEFGH", HotelId, 101, Guest.UserId, DateTime.Today, DateTime.Today.AddDays(2), true);

        var first = await _service.Handle(new WriteToFrontDeskCommand("¿A qué hora es el desayuno?", Guest));
        var second = await _service.Handle(new WriteToFrontDeskCommand("Gracias", Guest));

        second.ConversationId.Should().Be(first.ConversationId);
        _context.Set<Conversation>().Single().MessageCount.Should().Be(2);
        _push.Sent.Should().HaveCount(2).And.OnlyContain(push => push.UserIds.SequenceEqual(new[] { Reception.UserId })
                                                                  && push.Category == PushCategories.Message);
    }

    [Fact]
    public async Task TheFrontDeskAnswer_ShouldReachTheGuest()
    {
        _reservations.Stay = new GuestStay(7, "SS-ABCDEFGH", HotelId, 101, Guest.UserId, DateTime.Today, DateTime.Today.AddDays(2), true);
        var question = await _service.Handle(new WriteToFrontDeskCommand("¿Hay estacionamiento?", Guest));
        _push.Sent.Clear();

        await _service.Handle(new PostMessageCommand(question.ConversationId, "Sí, en el sótano.", Reception));

        _push.Sent.Should().ContainSingle().Which.UserIds.Should().Equal(Guest.UserId);
    }

    [Fact]
    public async Task AGuestWithoutAStay_ShouldNotWrite()
    {
        var act = () => _service.Handle(new WriteToFrontDeskCommand("Hola", Guest));

        await act.Should().ThrowAsync<NoStayForConversationException>();
    }

    [Fact]
    public async Task AnAreaChannel_ShouldReachEveryoneOfTheArea_ButTheSender()
    {
        await _service.Handle(new WriteToAreaCommand(HotelId, ServiceArea.Housekeeping, "Faltan sábanas en el piso 2", Housekeeper));

        _push.Sent.Should().ContainSingle().Which.UserIds.Should().Equal(OtherHousekeeper.UserId);
    }

    [Fact]
    public async Task AnAreaChannel_ShouldBeClosedToOtherAreas()
    {
        var act = () => _service.Handle(new WriteToAreaCommand(HotelId, ServiceArea.Housekeeping, "Hola", Maintenance));

        await act.Should().ThrowAsync<ConversationAccessDeniedException>();
    }

    [Fact]
    public void AGuestDesk_ShouldBeReadOnlyByItsGuestAndTheFrontDeskOfTheHotel()
    {
        var conversation = Conversation.WithFrontDesk(HotelId, Guest.UserId, DateTimeOffset.UtcNow);

        conversation.CanAccess(Guest).Should().BeTrue();
        conversation.CanAccess(Reception).Should().BeTrue();
        conversation.CanAccess(Housekeeper).Should().BeFalse();
        conversation.CanAccess(OtherHotelReception).Should().BeFalse();
        conversation.CanAccess(Guest with { UserId = 51 }).Should().BeFalse();
    }

    [Fact]
    public async Task ACommentOnATaskThatDoesNotExist_ShouldFindNothing()
    {
        var message = await _service.Handle(new CommentOnTaskCommand(ThreadSubject.CleaningTask, 999, "¿Quién la tiene?", Reception));

        message.Should().BeNull();
    }

    private sealed class FakeReservations : IRoomReservationsFacade
    {
        public GuestStay? Stay { get; set; }

        public Task<GuestStay?> FetchCurrentStayAsync(int guestUserId) =>
            Task.FromResult(Stay?.GuestUserId == guestUserId ? Stay : null);

        public Task<GuestStay?> FetchCurrentStayOfRoomAsync(int roomId) => Task.FromResult(Stay?.RoomId == roomId ? Stay : null);

        public Task<IReadOnlyDictionary<int, int>> CountActiveBookingsAsync(IReadOnlyCollection<int> roomIds) =>
            throw new NotSupportedException();

        public Task<bool> HasCurrentConfirmedStayAsync(int guestUserId, int roomId, DateTime day) => throw new NotSupportedException();
    }

    private sealed class FakeIam : IIamContextFacade
    {
        private static readonly List<UserContact> Staff =
        [
            new(Reception.UserId, "recepcion@roomtrack.pe", "Recepción", UserRoles.Reception),
            new(Housekeeper.UserId, "ana@roomtrack.pe", "Ana", UserRoles.Housekeeping),
            new(OtherHousekeeper.UserId, "beto@roomtrack.pe", "Beto", UserRoles.Housekeeping),
            new(Maintenance.UserId, "mario@roomtrack.pe", "Mario", UserRoles.Maintenance)
        ];

        public Task<IReadOnlyList<UserContact>> ListHotelStaffAsync(int hotelId, IReadOnlyCollection<string> roles) =>
            Task.FromResult<IReadOnlyList<UserContact>>(Staff.Where(contact => roles.Contains(contact.Role)).ToList());

        public Task<UserContact?> FetchUserContactAsync(int userId) =>
            Task.FromResult(Staff.FirstOrDefault(contact => contact.UserId == userId));

        public Task<int> FetchUserIdByEmail(string email) => throw new NotSupportedException();
        public Task<string> FetchEmailByUserId(int userId) => throw new NotSupportedException();

        public Task<ReissuedSession?> AssignHotelToAdministratorAsync(int userId, int hotelId, SessionContext currentSession) =>
            throw new NotSupportedException();
    }

    private sealed class Dispatcher(MessagePushHandler handler) : IDomainEventDispatcher
    {
        public async Task DispatchAsync(IEnumerable<IEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            foreach (var e in domainEvents.OfType<MessagePostedEvent>()) await handler.HandleAsync(e, cancellationToken);
        }
    }

    private sealed class RecordingPushSender : IPushSender
    {
        public List<PushMessage> Sent { get; } = [];

        public Task SendAsync(PushMessage message, CancellationToken cancellationToken = default)
        {
            if (message.UserIds.Count > 0) Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
