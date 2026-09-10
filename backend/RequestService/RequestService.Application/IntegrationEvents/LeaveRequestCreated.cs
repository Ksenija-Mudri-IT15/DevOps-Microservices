namespace Contracts.Events;

/// <summary>
/// Published by RequestService after a new request is successfully created.
/// Mirrored in NotificationService.Application/IntegrationEvents with an identical
/// namespace and shape so MassTransit's default RabbitMQ topology (which derives
/// exchange names from the message type's namespace + name) binds the two sides
/// without a shared library between the two solutions.
/// </summary>
public record LeaveRequestCreated(
    int RequestId,
    int EmployeeId,
    string RequestType,
    DateTime StartDate,
    DateTime EndDate,
    string Description);
