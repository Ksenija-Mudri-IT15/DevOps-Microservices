using Contracts.Events;
using MassTransit;
using MediatR;
using RequestService.Domain.Interfaces;

namespace RequestService.Application.Commands.ApproveRequest;

public record ApproveRequestCommand(int RequestId, string ApproverRole, bool Approve = true) : IRequest<bool>;

public class ApproveRequestHandler : IRequestHandler<ApproveRequestCommand, bool>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IPublishEndpoint _publishEndpoint;

    public ApproveRequestHandler(IRequestRepository requestRepository, IPublishEndpoint publishEndpoint)
    {
        _requestRepository = requestRepository;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<bool> Handle(ApproveRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);

        if (entity == null)
            throw new KeyNotFoundException($"Request with ID {request.RequestId} not found.");

        if (!request.Approve)
        {
            entity.Reject();
        }
        else if (request.ApproverRole == "Manager")
        {
            var managerAudit = new RequestService.Domain.ValueObjects.ManagerCredentialsVO("SystemManager", "AuditLog");
            entity.ApproveByManager(managerAudit);
        }
        else if (request.ApproverRole == "Admin")
        {
            var adminAudit = new RequestService.Domain.ValueObjects.AdminCredentialsVO("SystemAdmin", "AuditLog");
            entity.ApproveByAdmin(adminAudit);
        }
        else
        {
            throw new UnauthorizedAccessException("Only managers and admins can approve requests.");
        }

        _requestRepository.Update(entity);
        await _requestRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new LeaveRequestStatusChanged(
            entity.Id,
            entity.EmployeeId,
            entity.Status.Name,
            request.Approve ? request.ApproverRole : null), cancellationToken);

        return true;
    }
}
