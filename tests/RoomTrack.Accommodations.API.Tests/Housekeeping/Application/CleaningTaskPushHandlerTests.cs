using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RoomTrack.Accommodations.Application.Internal.QueryServices;
using RoomTrack.Accommodations.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Housekeeping.Application.Internal.EventHandlers;
using RoomTrack.Housekeeping.Domain.Model.Events;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Shared.Application.OutboundServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RoomTrack.Accommodations.API.Infrastructure.Persistence;
using RoomTrack.Contracts.Messaging;
using Xunit;

namespace RoomTrack.Tests.Housekeeping.Application;

/// <summary>Push notifications of the cleaning tasks (US-22).</summary>
public class CleaningTaskPushHandlerTests : IDisposable
{
    private const int Reception = 10;
    private const int Ana = 30;
    private const int Beto = 31;

    private readonly AccommodationsDbContext _context = new(new DbContextOptionsBuilder<AccommodationsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly RecordingPushSender _push = new();
    private readonly CleaningTaskPushHandler _handler;

    public CleaningTaskPushHandlerTests()
    {
        var rooms = new RoomRepository(_context);
        _handler = new CleaningTaskPushHandler(new RoomQueryService(rooms, new RoomStatusChangeRepository(_context)), _push);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ATaskThatBecameUrgent_ShouldNotifyItsAssignee_Highlighted()
    {
        await _handler.HandleAsync(new CleaningTaskPriorityChangedEvent(7, 1, 101, Ana, CleaningTaskPriority.Normal,
            CleaningTaskPriority.Urgent, Reception, DateTimeOffset.UtcNow), CancellationToken.None);

        var push = _push.Sent.Should().ContainSingle().Subject;
        push.UserIds.Should().Equal(Ana);
        push.Category.Should().Be(PushCategories.PriorityChanged);
        push.Urgent.Should().BeTrue();
        push.Data!["id"].Should().Be("7");
    }

    [Fact]
    public async Task APriorityChangeOfAnUnassignedTask_OrByItsAssignee_ShouldNotifyNobody()
    {
        await _handler.HandleAsync(new CleaningTaskPriorityChangedEvent(7, 1, 101, null, CleaningTaskPriority.Normal,
            CleaningTaskPriority.Urgent, Reception, DateTimeOffset.UtcNow), CancellationToken.None);
        await _handler.HandleAsync(new CleaningTaskPriorityChangedEvent(7, 1, 101, Ana, CleaningTaskPriority.Normal,
            CleaningTaskPriority.High, Ana, DateTimeOffset.UtcNow), CancellationToken.None);

        _push.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AReassignment_ShouldNotifyTheNewAndThePreviousAssignee()
    {
        await _handler.HandleAsync(new CleaningTaskAssignedEvent(7, 1, 101, Beto, Ana, Reception, DateTimeOffset.UtcNow),
            CancellationToken.None);

        _push.Sent.Select(push => push.UserIds.Single()).Should().Equal(Beto, Ana);
        _push.Sent.Should().OnlyContain(push => push.Category == PushCategories.CleaningTaskAssigned && !push.Urgent);
    }

    private sealed class RecordingPushSender : IPushSender
    {
        public List<PushMessage> Sent { get; } = [];

        public Task SendAsync(PushMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
