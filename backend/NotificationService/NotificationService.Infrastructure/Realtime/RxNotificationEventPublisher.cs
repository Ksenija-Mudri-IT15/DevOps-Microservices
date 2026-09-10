using System.Reactive.Linq;
using System.Reactive.Subjects;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Realtime
{
    /// <summary>
    /// Rx.NET-backed implementation of the reactive notification stream. A Subject is both
    /// an observer (Publish pushes into it) and an observable (Stream exposes it read-only) -
    /// registered as a singleton so every request/consumer in the process shares one stream.
    /// </summary>
    public sealed class RxNotificationEventPublisher : INotificationEventPublisher
    {
        private readonly Subject<NotificationDto> _subject = new();

        public IObservable<NotificationDto> Stream => _subject.AsObservable();

        public void Publish(NotificationDto notification) => _subject.OnNext(notification);
    }
}
