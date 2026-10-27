using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RoomTrack.Notifications.Application.Internal.CommandServices;
using RoomTrack.Notifications.Application.Internal.QueryServices;
using RoomTrack.Notifications.Domain.Model.Commands;
using RoomTrack.Notifications.Domain.Model.Exceptions;
using RoomTrack.Notifications.Domain.Model.Queries;
using RoomTrack.Notifications.Domain.Model.ValueObjects;
using RoomTrack.Notifications.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Domain.Model.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RoomTrack.Contracts.Messaging;
using RoomTrack.Identity.API.Infrastructure.Persistence;
using Xunit;

namespace RoomTrack.Identity.API.Tests.Notifications;

/// <summary>Devices and preferences of the push notifications (US-22) against an in-memory database.</summary>
public class PushNotificationServicesTests : IDisposable
{
    private const int Ana = 30;
    private const int Beto = 31;

    private readonly IdentityDbContext _context = new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly PushNotificationCommandService _commands;
    private readonly PushNotificationQueryService _queries;

    public PushNotificationServicesTests()
    {
        var devices = new PushDeviceRepository(_context);
        var preferences = new NotificationPreferencesRepository(_context);
        _commands = new PushNotificationCommandService(devices, preferences, new UnitOfWork(_context, new NoEvents()),
            TimeProvider.System);
        _queries = new PushNotificationQueryService(devices, preferences);
    }

    public void Dispose() => _context.Dispose();

    private Task<IReadOnlyList<string>> RecipientsAsync(string category, params int[] userIds) =>
        _queries.Handle(new GetPushRecipientTokensQuery(userIds, category));

    [Fact]
    public async Task RegisteringTheSameTokenAgain_ShouldKeepOneDevice_AndMoveItToWhoSignedIn()
    {
        var first = await _commands.Handle(new RegisterPushDeviceCommand(Ana, "token-1", PushPlatform.Android));
        var again = await _commands.Handle(new RegisterPushDeviceCommand(Beto, " token-1 ", PushPlatform.Android));

        again.Id.Should().Be(first.Id);
        again.UserId.Should().Be(Beto);
        (await RecipientsAsync(PushCategories.Message, Ana)).Should().BeEmpty();
        (await RecipientsAsync(PushCategories.Message, Beto)).Should().Equal("token-1");
    }

    [Fact]
    public async Task Recipients_ShouldSkipTheUsersThatTurnedTheKindOff()
    {
        await _commands.Handle(new RegisterPushDeviceCommand(Ana, "ana-phone", PushPlatform.Android));
        await _commands.Handle(new RegisterPushDeviceCommand(Beto, "beto-phone", PushPlatform.Ios));
        await _commands.Handle(new UpdateNotificationPreferencesCommand(Beto, [PushCategories.PriorityChanged]));

        (await RecipientsAsync(PushCategories.PriorityChanged, Ana, Beto)).Should().Equal("ana-phone");
        (await RecipientsAsync(PushCategories.CleaningTaskAssigned, Ana, Beto)).Should().BeEquivalentTo("ana-phone", "beto-phone");
    }

    [Fact]
    public async Task Preferences_WithAnUnknownKind_ShouldBeRefused()
    {
        var act = () => _commands.Handle(new UpdateNotificationPreferencesCommand(Ana, ["weather"]));

        (await act.Should().ThrowAsync<InvalidFieldException>()).Which.Violation.Code
            .Should().Be(NotificationErrorCodes.CategoryUnknown);
    }

    [Fact]
    public async Task Preferences_OfAUserWhoNeverChose_ShouldHaveEveryKindOn()
    {
        var preferences = await _queries.Handle(new GetNotificationPreferencesQuery(Ana));

        PushCategories.All.Should().OnlyContain(category => preferences.IsEnabled(category));
    }

    [Fact]
    public async Task Unregister_ShouldOnlyRemoveYourOwnDevice()
    {
        var device = await _commands.Handle(new RegisterPushDeviceCommand(Ana, "ana-phone", PushPlatform.Android));

        (await _commands.Handle(new UnregisterPushDeviceCommand(Beto, device.Id))).Should().BeFalse();
        (await _commands.Handle(new UnregisterPushDeviceCommand(Ana, device.Id))).Should().BeTrue();
        (await RecipientsAsync(PushCategories.Message, Ana)).Should().BeEmpty();
    }

    [Fact]
    public async Task RejectedTokens_ShouldBeForgotten()
    {
        await _commands.Handle(new RegisterPushDeviceCommand(Ana, "old-token", PushPlatform.Android));
        await _commands.Handle(new RegisterPushDeviceCommand(Ana, "new-token", PushPlatform.Android));

        var forgotten = await _commands.Handle(new ForgetRejectedPushDevicesCommand(["old-token", "unknown"]));

        forgotten.Should().Be(1);
        (await RecipientsAsync(PushCategories.Message, Ana)).Should().Equal("new-token");
    }

    [Theory]
    [InlineData("")]
    [InlineData("with space")]
    public async Task Register_WithAnInvalidToken_ShouldBeRefused(string token)
    {
        var act = () => _commands.Handle(new RegisterPushDeviceCommand(Ana, token, PushPlatform.Web));

        (await act.Should().ThrowAsync<InvalidFieldException>()).Which.Violation.Code.Should().Be(NotificationErrorCodes.TokenInvalid);
    }

    private sealed class NoEvents : IDomainEventDispatcher
    {
        public Task DispatchAsync(IEnumerable<IEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
