using Contracts.Events;
using MassTransit;
using Moq;
using NotificationService.Application.Consumers;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using Xunit;

namespace NotificationService.Domain.Tests.Application;

public class LeaveRequestCreatedConsumerTests
{
    [Fact]
    public async Task Consume_CreatesNotificationForRequestingEmployee()
    {
        var notificationService = new Mock<INotificationService>();
        notificationService
            .Setup(s => s.CreateNotificationAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDto());

        var message = new LeaveRequestCreated(
            RequestId: 12,
            EmployeeId: 2,
            RequestType: "Leave",
            StartDate: new DateTime(2026, 10, 1),
            EndDate: new DateTime(2026, 10, 5),
            Description: "Godisnji odmor");

        var context = new Mock<ConsumeContext<LeaveRequestCreated>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        var consumer = new LeaveRequestCreatedConsumer(notificationService.Object);

        await consumer.Consume(context.Object);

        notificationService.Verify(s => s.CreateNotificationAsync(
            It.Is<CreateNotificationRequest>(r =>
                r.RecipientId == 2 &&
                r.Message.Contains("#12") &&
                r.Message.Contains("Leave")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
