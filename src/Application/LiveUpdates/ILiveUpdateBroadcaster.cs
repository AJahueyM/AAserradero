namespace AntiguoAserradero.Application.LiveUpdates;

public interface ILiveUpdateBroadcaster : ILiveUpdatePublisher
{
    LiveUpdateSubscription Subscribe(string? excludedOriginatorUserId);
}
