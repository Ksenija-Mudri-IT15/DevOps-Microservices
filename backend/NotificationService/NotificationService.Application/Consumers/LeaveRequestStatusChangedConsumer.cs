using Contracts.Events;
using MassTransit;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.Application.Consumers;

public class LeaveRequestStatusChangedConsumer : IConsumer<LeaveRequestStatusChanged>
{
    private readonly INotificationService _notificationService;

    public LeaveRequestStatusChangedConsumer(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task Consume(ConsumeContext<LeaveRequestStatusChanged> context)
    {
        var message = context.Message;

        var text = message.Status switch
        {
            "Approved" => $"Your request #{message.RequestId} has been fully approved.",
            "PendingAdminApproval" => $"Your request #{message.RequestId} was approved by your manager and is awaiting admin approval.",
            "Rejected" => $"Your request #{message.RequestId} has been rejected.",
            _ => $"Your request #{message.RequestId} status changed to {message.Status}."
        };

        await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
        {
            RecipientId = message.EmployeeId,
            Message = text
        }, context.CancellationToken);
    }
}
