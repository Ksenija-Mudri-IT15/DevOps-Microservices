using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces
{
    /// <summary>
    /// Reactive communication channel (System.Reactive): every notification created in
    /// the app is pushed onto this stream so real-time subscribers (see
    /// NotificationService.API/Realtime/NotificationBroadcastService, which buffers and
    /// pushes over SignalR) can react to it, independent of the REST/RabbitMQ paths that
    /// create notifications in the first place.
    /// </summary>
    public interface INotificationEventPublisher
    {
        IObservable<NotificationDto> Stream { get; }

        void Publish(NotificationDto notification);
    }
}
