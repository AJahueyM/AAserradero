using System.Text.Json;
using AntiguoAserradero.Application.Auth;
using AntiguoAserradero.Application.LiveUpdates;

namespace AntiguoAserradero.Api.Endpoints;

public static class EventsEndpoints
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/events", async (HttpContext context, ICurrentUser currentUser, ILiveUpdateBroadcaster broadcaster) =>
            {
                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers.Connection = "keep-alive";
                context.Response.ContentType = "text/event-stream";

                await using var subscription = broadcaster.Subscribe(currentUser.Id);
                using var heartbeat = new PeriodicTimer(HeartbeatInterval);

                try
                {
                    while (!context.RequestAborted.IsCancellationRequested)
                    {
                        var readTask = subscription.Messages.ReadAsync(context.RequestAborted).AsTask();
                        var heartbeatTask = heartbeat.WaitForNextTickAsync(context.RequestAborted).AsTask();
                        var completed = await Task.WhenAny(readTask, heartbeatTask);

                        if (completed == readTask)
                        {
                            var message = await readTask;
                            // Emit a single default (unnamed) SSE event whose data carries the event
                            // type + payload, so the browser EventSource `onmessage` handler receives it.
                            var payload = string.IsNullOrWhiteSpace(message.JsonPayload) ? "{}" : message.JsonPayload;
                            var envelope = $"{{\"type\":{JsonSerializer.Serialize(message.Type)},\"payload\":{payload}}}";
                            await context.Response.WriteAsync($"data: {envelope}\n\n", context.RequestAborted);
                        }
                        else if (await heartbeatTask)
                        {
                            await context.Response.WriteAsync($": heartbeat {DateTimeOffset.UtcNow:O}\n\n", context.RequestAborted);
                        }

                        await context.Response.Body.FlushAsync(context.RequestAborted);
                    }
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                {
                    // Expected when the client disconnects the SSE stream; not an error.
                }
            })
            .RequireAuthorization()
            .WithName("GetEvents");

        return endpoints;
    }
}
