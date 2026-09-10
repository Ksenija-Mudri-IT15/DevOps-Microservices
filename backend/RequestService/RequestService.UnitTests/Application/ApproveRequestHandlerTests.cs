using Contracts.Events;
using MassTransit;
using Moq;
using RequestService.Application.Commands.ApproveRequest;
using RequestService.Domain.Entities;
using RequestService.Domain.Enums;
using RequestService.Domain.Interfaces;
using Xunit;

namespace RequestService.UnitTests.Application;

public class ApproveRequestHandlerTests
{
    private readonly Mock<IRequestRepository> _repository = new();
    private readonly Mock<IPublishEndpoint> _publishEndpoint = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private ApproveRequestHandler CreateHandler()
    {
        _repository.SetupGet(r => r.UnitOfWork).Returns(_unitOfWork.Object);
        return new ApproveRequestHandler(_repository.Object, _publishEndpoint.Object);
    }

    private static Request PendingRequest(int employeeId = 7) =>
        Request.SubmitLeaveRequest(employeeId, "Vacation", DateTime.Today.AddDays(1), DateTime.Today.AddDays(5), LeaveType.AnnualLeave);

    [Fact]
    public async Task Handle_AdminApproval_PublishesApprovedStatus()
    {
        var request = PendingRequest();
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var handler = CreateHandler();

        await handler.Handle(new ApproveRequestCommand(1, "Admin"), CancellationToken.None);

        _publishEndpoint.Verify(p => p.Publish(
            It.Is<LeaveRequestStatusChanged>(e =>
                e.RequestId == request.Id &&
                e.EmployeeId == 7 &&
                e.Status == "Approved" &&
                e.ApproverRole == "Admin"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Rejection_PublishesRejectedStatusWithNoApproverRole()
    {
        var request = PendingRequest();
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var handler = CreateHandler();

        await handler.Handle(new ApproveRequestCommand(1, "Admin", Approve: false), CancellationToken.None);

        _publishEndpoint.Verify(p => p.Publish(
            It.Is<LeaveRequestStatusChanged>(e => e.Status == "Rejected" && e.ApproverRole == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRequestNotFound_DoesNotPublish()
    {
        _repository.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((Request?)null);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(new ApproveRequestCommand(404, "Admin"), CancellationToken.None));

        _publishEndpoint.Verify(p => p.Publish(It.IsAny<LeaveRequestStatusChanged>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
