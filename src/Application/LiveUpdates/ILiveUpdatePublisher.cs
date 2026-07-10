namespace AntiguoAserradero.Application.LiveUpdates;

public interface ILiveUpdatePublisher
{
    ValueTask PublishAsync(LiveUpdateMessage message, CancellationToken cancellationToken = default);
}
