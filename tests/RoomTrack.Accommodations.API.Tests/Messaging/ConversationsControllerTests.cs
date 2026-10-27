using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.GuestRequests.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Housekeeping.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Incidents.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Messaging.Application.Internal.CommandServices;
using RoomTrack.Messaging.Application.Internal.QueryServices;
using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Messaging.Domain.Model.Entities;
using RoomTrack.Messaging.Domain.Model.Exceptions;
using RoomTrack.Messaging.Domain.Model.ValueObjects;
using RoomTrack.Messaging.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Messaging.Interfaces.REST;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomTrack.Accommodations.API.Infrastructure.Persistence;
using Xunit;

namespace RoomTrack.Tests.Messaging;

/// <summary>
///     Who may read and write which conversations (US-12, US-20), through the controller against the real services and
///     an in-memory database. Somebody outside a conversation gets a 404: they do not learn that it exists.
/// </summary>
/// <remarks>The role policies of the endpoints are applied by ASP.NET Core, not here: these tests cover the checks of the controller and the aggregate.</remarks>
public class ConversationsControllerTests : IDisposable
{
    private const int HotelId = 1;
    private const int OtherHotelId = 2;
    private const int GuestId = 50;
    private const int OtherGuestId = 51;
    private const int ReceptionId = 10;
    private const int OtherHotelReceptionId = 11;
    private const int HousekeeperId = 30;
    private const int MaintenanceId = 20;

    private readonly AccommodationsDbContext _context = new(new DbContextOptionsBuilder<AccommodationsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly FakeReservations _reservations = new();
    private readonly MessagingCommandService _commandService;
    private readonly MessagingQueryService _queryService;

    public ConversationsControllerTests()
    {
        var conversations = new ConversationRepository(_context);
        var messages = new ConversationMessageRepository(_context);
        _queryService = new MessagingQueryService(conversations, messages);
        _commandService = new MessagingCommandService(conversations, messages, _reservations, new CleaningTaskRepository(_context),
            new IncidentRepository(_context), new GuestRequestRepository(_context), new UnitOfWork(_context, new NoEvents()),
            TimeProvider.System);
        _reservations.Stays.Add(new GuestStay(7, "SS-ABCDEFGH", HotelId, 101, GuestId, DateTime.Today, DateTime.Today.AddDays(2), true));
        _reservations.Stays.Add(new GuestStay(8, "SS-IJKLMNOP", OtherHotelId, 201, OtherGuestId, DateTime.Today,
            DateTime.Today.AddDays(2), true));
    }

    public void Dispose() => _context.Dispose();

    private ConversationsController AsGuest(int userId = GuestId) => Controller(userId, UserRoles.Guest, null);
    private ConversationsController AsReception() => Controller(ReceptionId, UserRoles.Reception, HotelId);
    private ConversationsController AsOtherHotelReception() => Controller(OtherHotelReceptionId, UserRoles.Reception, OtherHotelId);
    private ConversationsController AsHousekeeper() => Controller(HousekeeperId, UserRoles.Housekeeping, HotelId);
    private ConversationsController AsMaintenance() => Controller(MaintenanceId, UserRoles.Maintenance, HotelId);

    private ConversationsController Controller(int userId, string role, int? hotelId)
    {
        List<Claim> claims =
        [
            new(IamClaimTypes.UserId, userId.ToString()),
            new(IamClaimTypes.Username, $"user{userId}@roomtrack.pe"),
            new(IamClaimTypes.Role, role)
        ];
        if (hotelId is { } id) claims.Add(new Claim(IamClaimTypes.HotelId, id.ToString()));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", IamClaimTypes.Username, IamClaimTypes.Role));
        return new ConversationsController(_commandService, _queryService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
        };
    }

    private static MessageResource Created(IActionResult result) =>
        (MessageResource)result.Should().BeOfType<CreatedAtActionResult>().Subject.Value!;

    /// <summary>The conversation of the guest with the front desk, opened with their first message.</summary>
    private async Task<int> GuestDeskAsync(int guestId = GuestId) =>
        Created(await AsGuest(guestId).WriteToFrontDesk(new PostMessageResource("¿A qué hora es el desayuno?"))).ConversationId;

    [Fact]
    public async Task OnlyAGuest_ShouldWriteToTheFrontDesk()
    {
        (await AsReception().WriteToFrontDesk(new PostMessageResource("Hola"))).Should().BeOfType<ForbidResult>();
        _context.Set<ConversationMessage>().Should().BeEmpty();
    }

    [Fact]
    public async Task AGuestDesk_ShouldBeHiddenFromAnotherGuest_TheStaffOfTheAreas_AndAnotherHotel()
    {
        var conversationId = await GuestDeskAsync();

        (await AsGuest(OtherGuestId).GetConversation(conversationId, null)).Should().BeOfType<NotFoundResult>();
        (await AsHousekeeper().GetConversation(conversationId, null)).Should().BeOfType<NotFoundResult>();
        (await AsOtherHotelReception().GetConversation(conversationId, null)).Should().BeOfType<NotFoundResult>();
        (await AsGuest().GetConversation(conversationId, null)).Should().BeOfType<OkObjectResult>();
        (await AsReception().GetConversation(conversationId, null)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SomebodyOutsideAConversation_ShouldNotWriteInIt()
    {
        var conversationId = await GuestDeskAsync();

        (await AsGuest(OtherGuestId).PostMessage(conversationId, new PostMessageResource("Hola")))
            .Should().BeOfType<NotFoundResult>();
        (await AsOtherHotelReception().PostMessage(conversationId, new PostMessageResource("Hola")))
            .Should().BeOfType<NotFoundResult>();

        _context.Set<ConversationMessage>().Should().ContainSingle();
    }

    [Fact]
    public async Task AGuest_ShouldListOnlyTheirOwnConversations()
    {
        var mine = await GuestDeskAsync();
        await GuestDeskAsync(OtherGuestId);

        var result = await AsGuest().GetConversations(OtherHotelId);

        result.Should().BeOfType<OkObjectResult>().Which.Value.As<IEnumerable<ConversationResource>>()
            .Should().ContainSingle().Which.Id.Should().Be(mine);
    }

    [Fact]
    public async Task TheFrontDesk_ShouldListOnlyTheConversationsOfTheirHotel()
    {
        var ofMyHotel = await GuestDeskAsync();
        await GuestDeskAsync(OtherGuestId);

        var result = await AsReception().GetConversations(OtherHotelId);

        result.Should().BeOfType<OkObjectResult>().Which.Value.As<IEnumerable<ConversationResource>>()
            .Select(conversation => conversation.Id).Should().Equal(ofMyHotel);
    }

    [Fact]
    public async Task TheStaff_ShouldWriteToTheAreaChannelOfTheirOwnHotel_WhateverHotelTheySend()
    {
        var message = Created(await AsHousekeeper().WriteToArea(ServiceArea.Housekeeping, new PostMessageResource("Faltan sábanas"),
            hotelId: OtherHotelId));

        _context.Set<Conversation>().Single(conversation => conversation.Id == message.ConversationId).HotelId.Should().Be(HotelId);
    }

    [Fact]
    public async Task TheChannelOfAnArea_ShouldBeClosedToTheStaffOfOtherAreas()
    {
        var message = Created(await AsHousekeeper().WriteToArea(ServiceArea.Housekeeping, new PostMessageResource("Faltan sábanas"), null));

        (await AsMaintenance().GetConversation(message.ConversationId, null)).Should().BeOfType<NotFoundResult>();
        var write = () => AsMaintenance().WriteToArea(ServiceArea.Housekeeping, new PostMessageResource("Hola"), null);
        await write.Should().ThrowAsync<ConversationAccessDeniedException>();
    }

    [Fact]
    public async Task StaffWithoutAHotel_ShouldNotWriteToAnAreaChannel()
    {
        var result = await Controller(99, UserRoles.Housekeeping, null)
            .WriteToArea(ServiceArea.Housekeeping, new PostMessageResource("Hola"), HotelId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task TheStaffOfAnotherHotel_ShouldNotReadNorCommentOnATask()
    {
        var request = GuestRequest.SendByGuest(HotelId, 101, 7, "Toallas extra", null, null,
            new GuestRequestActor(GuestId, null, UserRoles.Guest), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _context.Add(request);
        await _context.SaveChangesAsync();
        Created(await AsHousekeeper().CommentOnTask(ThreadSubject.GuestRequest, request.Id, new PostMessageResource("¿Quién la tiene?")));

        (await AsOtherHotelReception().GetTaskThread(ThreadSubject.GuestRequest, request.Id)).Should().BeOfType<NotFoundResult>();
        var comment = () => AsOtherHotelReception().CommentOnTask(ThreadSubject.GuestRequest, request.Id, new PostMessageResource("Yo"));
        await comment.Should().ThrowAsync<ConversationAccessDeniedException>();
        (await AsMaintenance().GetTaskThread(ThreadSubject.GuestRequest, request.Id)).Should().BeOfType<OkObjectResult>();
    }

    private sealed class FakeReservations : IRoomReservationsFacade
    {
        public List<GuestStay> Stays { get; } = [];

        public Task<GuestStay?> FetchCurrentStayAsync(int guestUserId) =>
            Task.FromResult(Stays.FirstOrDefault(stay => stay.GuestUserId == guestUserId));

        public Task<GuestStay?> FetchCurrentStayOfRoomAsync(int roomId) =>
            Task.FromResult(Stays.FirstOrDefault(stay => stay.RoomId == roomId));

        public Task<IReadOnlyDictionary<int, int>> CountActiveBookingsAsync(IReadOnlyCollection<int> roomIds) =>
            throw new NotSupportedException();

        public Task<bool> HasCurrentConfirmedStayAsync(int guestUserId, int roomId, DateTime day) => throw new NotSupportedException();
    }

    /// <summary>The notifications are not what these tests are about.</summary>
    private sealed class NoEvents : IDomainEventDispatcher
    {
        public Task DispatchAsync(IEnumerable<IEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
