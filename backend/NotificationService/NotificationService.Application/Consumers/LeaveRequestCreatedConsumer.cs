using Contracts.Events;
using MassTransit;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.Application.Consumers;

public class LeaveRequestCreatedConsumer : IConsumer<LeaveRequestCreated>
{
    private readonly INotificationService _notificationService;

    public LeaveRequestCreatedConsumer(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task Consume(ConsumeContext<LeaveRequestCreated> context)
    {
        var message = context.Message;

        await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
        {
            RecipientId = message.EmployeeId,
            Message = $"Your {message.RequestType} request #{message.RequestId} " +
                      $"({message.StartDate:d} - {message.EndDate:d}) has been submitted and is awaiting approval."
        }, context.CancellationToken);
    }
}
