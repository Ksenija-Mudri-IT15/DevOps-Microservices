namespace Contracts.Events;

/// <summary>
/// Published by RequestService whenever a request's status changes (approved
/// at any stage, or rejected). Mirrored in NotificationService.Application/IntegrationEvents.
/// </summary>
public record LeaveRequestStatusChanged(
    int RequestId,
    int EmployeeId,
    string Status,
    string? ApproverRole);
