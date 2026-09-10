using Contracts.Events;
using MassTransit;
using Moq;
using NotificationService.Application.Consumers;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using Xunit;

namespace NotificationService.Domain.Tests.Application;

public class LeaveRequestStatusChangedConsumerTests
{
    private static Mock<ConsumeContext<LeaveRequestStatusChanged>> ContextFor(LeaveRequestStatusChanged message)
    {
        var context = new Mock<ConsumeContext<LeaveRequestStatusChanged>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Theory]
    [InlineData("Approved", "fully approved")]
    [InlineData("PendingAdminApproval", "awaiting admin approval")]
    [InlineData("Rejected", "rejected")]
    public async Task Consume_MapsStatusToRecipientFacingMessage(string status, string expectedFragment)
    {
        var notificationService = new Mock<INotificationService>();
        notificationService
            .Setup(s => s.CreateNotificationAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDto());

        var message = new LeaveRequestStatusChanged(RequestId: 12, EmployeeId: 2, Status: status, ApproverRole: "Admin");
        var context = ContextFor(message);

        var consumer = new LeaveRequestStatusChangedConsumer(notificationService.Object);

        await consumer.Consume(context.Object);

        notificationService.Verify(s => s.CreateNotificationAsync(
            It.Is<CreateNotificationRequest>(r => r.RecipientId == 2 && r.Message.Contains(expectedFragment)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
