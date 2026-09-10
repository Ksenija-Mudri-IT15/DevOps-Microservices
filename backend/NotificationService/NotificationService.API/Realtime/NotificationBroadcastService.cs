using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;
using NotificationService.API.Hubs;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Realtime;

/// <summary>
/// Bridges the Rx.NET notification stream to SignalR. Buffers the stream into 1-second
/// windows before broadcasting - a real use of a reactive operator (backpressure/batching),
/// not just a pass-through: several notifications created in quick succession (e.g. a batch
/// of RabbitMQ-consumed events) reach each client as one push instead of one message each.
/// </summary>
public class NotificationBroadcastService : BackgroundService
{
    private readonly INotificationEventPublisher _eventPublisher;
    private readonly IHubContext<NotificationHub> _hubContext;
    private IDisposable? _subscription;

    public NotificationBroadcastService(INotificationEventPublisher eventPublisher, IHubContext<NotificationHub> hubContext)
    {
        _eventPublisher = eventPublisher;
        _hubContext = hubContext;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _subscription = _eventPublisher.Stream
            .Buffer(TimeSpan.FromSeconds(1))
            .Where(batch => batch.Count > 0)
            .SelectMany(batch => batch.GroupBy(n => n.RecipientId))
            .Subscribe(group =>
            {
                var recipientId = group.Key;
                var notifications = group.ToList();

                _ = _hubContext.Clients
                    .Group(NotificationHub.GroupName(recipientId))
                    .SendAsync("ReceiveNotifications", notifications, stoppingToken);
            });

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _subscription?.Dispose();
        base.Dispose();
    }
}
