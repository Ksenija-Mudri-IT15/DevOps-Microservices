using NotificationService.Application.DTOs;
using NotificationService.Infrastructure.Realtime;
using Xunit;

namespace NotificationService.Domain.Tests.Infrastructure;

public class RxNotificationEventPublisherTests
{
    [Fact]
    public void Publish_DeliversNotificationToSubscribersOfTheStream()
    {
        var publisher = new RxNotificationEventPublisher();
        var received = new List<NotificationDto>();

        using var subscription = publisher.Stream.Subscribe(received.Add);

        var dto = new NotificationDto { NotificationId = 1, RecipientId = 2, Message = "Test" };
        publisher.Publish(dto);

        Assert.Single(received);
        Assert.Same(dto, received[0]);
    }

    [Fact]
    public void Publish_BeforeAnySubscriber_IsNotDeliveredLater()
    {
        // Subject semantics: it is a hot observable, not a replay - a value published before
        // a subscription exists is simply missed by that subscriber.
        var publisher = new RxNotificationEventPublisher();

        publisher.Publish(new NotificationDto { NotificationId = 1, RecipientId = 2, Message = "Missed" });

        var received = new List<NotificationDto>();
        using var subscription = publisher.Stream.Subscribe(received.Add);

        Assert.Empty(received);
    }
}
