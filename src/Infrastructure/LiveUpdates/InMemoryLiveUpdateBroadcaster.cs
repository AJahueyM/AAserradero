using System.Collections.Concurrent;
using System.Threading.Channels;
using AntiguoAserradero.Application.LiveUpdates;

namespace AntiguoAserradero.Infrastructure.LiveUpdates;

/// <summary>
/// Single-replica in-memory SSE broadcaster. Replace with a distributed backplane before scaling out.
/// </summary>
public sealed class InMemoryLiveUpdateBroadcaster : ILiveUpdateBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();

    public LiveUpdateSubscription Subscribe(string? excludedOriginatorUserId)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<LiveUpdateMessage>(new BoundedChannelOptions(128)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        _subscribers[id] = new Subscriber(excludedOriginatorUserId, channel.Writer);
        return new LiveUpdateSubscription(channel.Reader, () =>
        {
            if (_subscribers.TryRemove(id, out var subscriber))
            {
                subscriber.Writer.TryComplete();
            }

            return ValueTask.CompletedTask;
        });
    }

    public ValueTask PublishAsync(LiveUpdateMessage message, CancellationToken cancellationToken = default)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            if (message.OriginatorUserId is not null &&
                string.Equals(message.OriginatorUserId, subscriber.ExcludedOriginatorUserId, StringComparison.Ordinal))
            {
                continue;
            }

            subscriber.Writer.TryWrite(message);
        }

        return ValueTask.CompletedTask;
    }

    private sealed record Subscriber(string? ExcludedOriginatorUserId, ChannelWriter<LiveUpdateMessage> Writer);
}
