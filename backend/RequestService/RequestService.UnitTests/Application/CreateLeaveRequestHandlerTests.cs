using Contracts.Events;
using MassTransit;
using Moq;
using RequestService.Application.Commands.CreateLeaveRequest;
using RequestService.Application.Services;
using RequestService.Domain.Entities;
using RequestService.Domain.Enums;
using RequestService.Domain.Interfaces;
using Xunit;

namespace RequestService.UnitTests.Application;

public class CreateLeaveRequestHandlerTests
{
    private readonly Mock<IRequestRepository> _repository = new();
    private readonly Mock<IEmployeeServiceClient> _employeeServiceClient = new();
    private readonly Mock<IPublishEndpoint> _publishEndpoint = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private CreateLeaveRequestHandler CreateHandler()
    {
        _repository.SetupGet(r => r.UnitOfWork).Returns(_unitOfWork.Object);
        return new CreateLeaveRequestHandler(_repository.Object, _employeeServiceClient.Object, _publishEndpoint.Object);
    }

    [Fact]
    public async Task Handle_WithValidLeaveRequest_PublishesLeaveRequestCreated()
    {
        _employeeServiceClient.Setup(c => c.ValidateEmployeeExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(r => r.GetRequestsByEmployeeIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Request>());

        var command = new CreateLeaveRequestCommand(
            EmployeeId: 1,
            Description: "Godisnji odmor",
            StartDate: DateTime.Today.AddDays(1),
            EndDate: DateTime.Today.AddDays(5),
            LeaveType: LeaveType.AnnualLeave,
            Type: RequestType.Leave);

        var handler = CreateHandler();

        await handler.Handle(command, CancellationToken.None);

        _publishEndpoint.Verify(p => p.Publish(
            It.Is<LeaveRequestCreated>(e =>
                e.EmployeeId == 1 &&
                e.RequestType == "Leave" &&
                e.StartDate == command.StartDate &&
                e.EndDate == command.EndDate),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmployeeDoesNotExist_DoesNotPublish()
    {
        _employeeServiceClient.Setup(c => c.ValidateEmployeeExistsAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateLeaveRequestCommand(
            EmployeeId: 99,
            Description: "Godisnji odmor",
            StartDate: DateTime.Today.AddDays(1),
            EndDate: DateTime.Today.AddDays(5),
            LeaveType: LeaveType.AnnualLeave,
            Type: RequestType.Leave);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(command, CancellationToken.None));

        _publishEndpoint.Verify(p => p.Publish(It.IsAny<LeaveRequestCreated>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithOverlappingDates_DoesNotPublish()
    {
        _employeeServiceClient.Setup(c => c.ValidateEmployeeExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var existing = Request.SubmitLeaveRequest(1, "Existing", DateTime.Today, DateTime.Today.AddDays(10), LeaveType.AnnualLeave);
        _repository.Setup(r => r.GetRequestsByEmployeeIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existing });

        var command = new CreateLeaveRequestCommand(
            EmployeeId: 1,
            Description: "Overlapping",
            StartDate: DateTime.Today.AddDays(2),
            EndDate: DateTime.Today.AddDays(4),
            LeaveType: LeaveType.AnnualLeave,
            Type: RequestType.Leave);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));

        _publishEndpoint.Verify(p => p.Publish(It.IsAny<LeaveRequestCreated>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
