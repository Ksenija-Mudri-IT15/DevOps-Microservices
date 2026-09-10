using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Aggregates.NotificationAggregate;
using NotificationService.Domain.Exceptions;

namespace NotificationService.Application.Services
{
    public class NotificationAppService : INotificationService
    {
        private readonly INotificationRepository _repository;
        private readonly INotificationEventPublisher _eventPublisher;

        public NotificationAppService(INotificationRepository repository, INotificationEventPublisher eventPublisher)
        {
            _repository = repository;
            _eventPublisher = eventPublisher;
        }

        public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
        {
            var notification = Notification.CreateNotification(request.RecipientId, request.Message);

            await _repository.AddAsync(notification, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(notification);

            // Reactive communication: every new notification (whether it came from a direct
            // REST call or a RabbitMQ consumer) is pushed onto the observable stream for
            // real-time subscribers - see INotificationEventPublisher.
            _eventPublisher.Publish(dto);

            return dto;
        }

        public async Task<IEnumerable<NotificationDto>> GetNotificationsForUserAsync(int recipientId, CancellationToken cancellationToken = default)
        {
            var notifications = await _repository.GetAllByRecipientIdAsync(recipientId, cancellationToken);
            return notifications.Select(MapToDto);
        }

        public async Task<NotificationDto> GetNotificationByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var notification = await GetNotificationOrElseThrowAsync(id, cancellationToken);
            return MapToDto(notification);
        }

        public async Task MarkAsReadAsync(int id, CancellationToken cancellationToken = default)
        {
            var notification = await GetNotificationOrElseThrowAsync(id, cancellationToken);
            notification.MarkAsRead();
            
            await _repository.UpdateAsync(notification, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkAsUnreadAsync(int id, CancellationToken cancellationToken = default)
        {
            var notification = await GetNotificationOrElseThrowAsync(id, cancellationToken);
            notification.MarkAsUnread();
            
            await _repository.UpdateAsync(notification, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteNotificationAsync(int id, CancellationToken cancellationToken = default)
        {
            var notification = await GetNotificationOrElseThrowAsync(id, cancellationToken);
            
            await _repository.DeleteAsync(notification, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        private async Task<Notification> GetNotificationOrElseThrowAsync(int id, CancellationToken cancellationToken)
        {
            var notification = await _repository.GetByIdAsync(id, cancellationToken);
            if (notification == null)
            {
                // In a real app, you might use a generic NotFoundException, here we reuse DomainException for simplicity.
                throw new DomainException($"Notification with Id {id} was not found.");
            }
            return notification;
        }

        private NotificationDto MapToDto(Notification notification)
        {
            return new NotificationDto
            {
                NotificationId = notification.NotificationId,
                RecipientId = notification.RecipientId,
                Message = notification.Message,
                Date = notification.Date,
                ReadStatus = notification.ReadStatus
            };
        }
    }
}
