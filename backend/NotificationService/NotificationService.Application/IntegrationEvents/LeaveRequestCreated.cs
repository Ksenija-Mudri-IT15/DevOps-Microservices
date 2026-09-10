namespace Contracts.Events;

/// <summary>
/// Consumed side of the event published by RequestService.Application/IntegrationEvents.
/// Namespace and shape must stay identical on both sides — MassTransit's default
/// RabbitMQ topology binds publisher and consumer by the message type's
/// namespace + name, not by shared assembly identity.
/// </summary>
public record LeaveRequestCreated(
    int RequestId,
    int EmployeeId,
    string RequestType,
    DateTime StartDate,
    DateTime EndDate,
    string Description);
