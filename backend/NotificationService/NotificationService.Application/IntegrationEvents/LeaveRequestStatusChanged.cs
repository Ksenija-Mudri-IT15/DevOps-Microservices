namespace Contracts.Events;

/// <summary>
/// Consumed side of the event published by RequestService.Application/IntegrationEvents.
/// </summary>
public record LeaveRequestStatusChanged(
    int RequestId,
    int EmployeeId,
    string Status,
    string? ApproverRole);
