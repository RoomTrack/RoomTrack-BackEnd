using System;
using System.Reflection;
using System.Threading;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using RoomTrack.Analytics.API.Infrastructure.Persistence;

namespace RoomTrack.Analytics.API.Tests;

/// <summary>Database and MassTransit doubles shared by the Analytics tests.</summary>
internal static class AnalyticsTestSupport
{
    /// <summary>A fresh in-memory database of the Analytics service.</summary>
    public static DbContextOptions<AnalyticsDbContext> NewDatabase() =>
        new DbContextOptionsBuilder<AnalyticsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    /// <summary>
    ///     A <see cref="ConsumeContext{T}"/> that only carries <paramref name="message"/>: the consumers of the read
    ///     model read the message and the cancellation token, nothing else.
    /// </summary>
    public static ConsumeContext<T> ContextFor<T>(T message) where T : class
    {
        var context = DispatchProxy.Create<ConsumeContext<T>, MessageOnlyConsumeContext<T>>();
        ((MessageOnlyConsumeContext<T>)(object)context).Message = message;
        return context;
    }

    public class MessageOnlyConsumeContext<T> : DispatchProxy where T : class
    {
        public T Message { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "get_Message" => Message,
            "get_CancellationToken" => CancellationToken.None,
            _ => throw new NotSupportedException($"{targetMethod?.Name} is not used by the read model consumers.")
        };
    }
}
