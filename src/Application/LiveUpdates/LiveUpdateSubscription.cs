using System.Threading.Channels;

namespace AntiguoAserradero.Application.LiveUpdates;

public sealed class LiveUpdateSubscription : IAsyncDisposable
{
    private readonly Func<ValueTask> _unsubscribe;

    public LiveUpdateSubscription(ChannelReader<LiveUpdateMessage> messages, Func<ValueTask> unsubscribe)
    {
        Messages = messages;
        _unsubscribe = unsubscribe;
    }

    public ChannelReader<LiveUpdateMessage> Messages { get; }

    public ValueTask DisposeAsync() => _unsubscribe();
}
