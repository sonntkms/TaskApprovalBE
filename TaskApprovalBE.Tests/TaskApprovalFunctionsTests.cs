
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Moq;
using TaskApprovalBE.Models;
using TaskApprovalBE.Services;
using TaskApprovalBE.Tests.Helpers;


namespace TaskApprovalBE.Tests;

public class TaskApprovalFunctionsTests
{
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ILogger<TaksApprovalFunctions>> _mockLogger;
    private readonly TaksApprovalFunctions _functions;

    public TaskApprovalFunctionsTests()
    {
        _mockEmailService = new Mock<IEmailService>();
        _mockLogger = new Mock<ILogger<TaksApprovalFunctions>>();
        _functions = new TaksApprovalFunctions(_mockEmailService.Object, _mockLogger.Object);
    }

    private static Mock<HttpRequest> CreateMockHttpRequest(string content)
    {
        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var mockRequest = new Mock<HttpRequest>();
        mockRequest.Setup(r => r.Body).Returns(memoryStream);
        return mockRequest;
    }

    [Fact]
    public async Task SendStartedEmail_ShouldSendCorrectEmail()
    {
        // Arrange
        var request = new ApprovalRequest
        {
            Id = Guid.NewGuid().ToString(),
            UserEmail = "user@example.com",
            TaskName = "Test Task"
        };
        var mockFunctionContext = new Mock<FunctionContext>();

        // Act
        await _functions.SendStartedEmail(request, mockFunctionContext.Object);

        // Assert
        _mockEmailService.Verify(
            s => s.SendEmailAsync(
                "user@example.com",
                $"Approval Process Started for {request.TaskName}",
                It.Is<string>(content => content.Contains(request.Id) && content.Contains(request.TaskName))
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task SendApprovedEmail_ShouldSendCorrectEmail()
    {
        // Arrange
        var request = new ApprovalRequest
        {
            Id = Guid.NewGuid().ToString(),
            UserEmail = "user@example.com",
            TaskName = "Test Task"
        };
        var mockFunctionContext = new Mock<FunctionContext>();

        // Act
        await _functions.SendApprovedEmail(request, mockFunctionContext.Object);

        // Assert
        _mockEmailService.Verify(
            s => s.SendEmailAsync(
                "user@example.com",
                $"Your Request Has Been Approved for {request.TaskName}",
                It.Is<string>(content => content.Contains(request.Id) && content.Contains(request.TaskName))
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task SendRejectedEmail_ShouldSendCorrectEmail()
    {
        // Arrange
        var request = new ApprovalRequest
        {
            Id = Guid.NewGuid().ToString(),
            UserEmail = "user@example.com",
            TaskName = "Test Task"
        };
        var mockFunctionContext = new Mock<FunctionContext>();

        // Act
        await _functions.SendRejectedEmail(request, mockFunctionContext.Object);

        // Assert
        _mockEmailService.Verify(
            s => s.SendEmailAsync(
                "user@example.com",
                $"Your Request Has Been Rejected for {request.TaskName}",
                It.Is<string>(content => content.Contains(request.Id) && content.Contains(request.TaskName))
            ),
            Times.Once
        );
    }
    
    [Fact]
    public async Task RunOrchestrationAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockContext = new Mock<TaskOrchestrationContext>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _functions.RunOrchestrationAsync(mockContext.Object, null));
    }

    [Fact]
    public async Task RunOrchestrationAsync_WhenApproved_ShouldCallCorrectActivities()
    {
        // Arrange
        var request = new ApprovalRequest
        {
            Id = "test-id",
            UserEmail = "user@example.com",
            TaskName = "Test Task"
        };

        var mockContext = new Mock<TaskOrchestrationContext>();

        mockContext
            .Setup(ctx => ctx.CallActivityAsync(nameof(TaksApprovalFunctions.SendStartedEmail), request, null))
            .Returns(Task.CompletedTask);

        mockContext
            .Setup(ctx => ctx.WaitForExternalEvent<ApprovalResult>(TaksApprovalFunctions.APPROVAL_EVENT_NAME, default(CancellationToken)))
            .ReturnsAsync(new ApprovalResult { IsApproved = true });

        mockContext
            .Setup(ctx => ctx.CallActivityAsync(nameof(TaksApprovalFunctions.SendApprovedEmail), request, null))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _functions.RunOrchestrationAsync(mockContext.Object, request);

        // Assert
        Assert.Equal(ApprovalOrchestrationResult.APPROVED, result);
        mockContext.Verify(ctx => ctx.CallActivityAsync(nameof(TaksApprovalFunctions.SendStartedEmail), request, null), Times.Once);
        mockContext.Verify(ctx => ctx.CallActivityAsync(nameof(TaksApprovalFunctions.SendApprovedEmail), request, null), Times.Once);
        mockContext.Verify(ctx => ctx.CallActivityAsync(nameof(TaksApprovalFunctions.SendRejectedEmail), request, null), Times.Never);
    }

    [Fact]
    public async Task RunOrchestrationAsync_WhenRejected_ShouldCallCorrectActivities()
    {
        // Arrange
        var request = new ApprovalRequest
        {
            Id = "test-id",
            UserEmail = "user@example.com",
            TaskName = "Test Task"
        };

        var mockContext = new Mock<TaskOrchestrationContext>();

        mockContext
            .Setup(ctx => ctx.CallActivityAsync(nameof(TaksApprovalFunctions.SendStartedEmail), request, null))
            .Returns(Task.CompletedTask);

        mockContext
            .Setup(ctx => ctx.WaitForExternalEvent<ApprovalResult>(TaksApprovalFunctions.APPROVAL_EVENT_NAME, default(CancellationToken)))
            .ReturnsAsync(new ApprovalResult { IsApproved = false });

        mockContext
            .Setup(ctx => ctx.CallActivityAsync(nameof(TaksApprovalFunctions.SendRejectedEmail), request, null))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _functions.RunOrchestrationAsync(mockContext.Object, request);

        // Assert
        Assert.Equal(ApprovalOrchestrationResult.REJECTED, result);
        mockContext.Verify(ctx => ctx.CallActivityAsync(nameof(TaksApprovalFunctions.SendStartedEmail), request, null), Times.Once);
        mockContext.Verify(ctx => ctx.CallActivityAsync(nameof(TaksApprovalFunctions.SendApprovedEmail), request, null), Times.Never);
        mockContext.Verify(ctx => ctx.CallActivityAsync(nameof(TaksApprovalFunctions.SendRejectedEmail), request, null), Times.Once);
    }

    [Fact]
    public async Task StartApproval_WithValidRequest_ShouldStartOrchestration()
    {
        // Arrange
        var request = new ApprovalRequest
        {
            Id = "test-id",
            UserEmail = "user@example.com",
            TaskName = "Test Task"
        };

        var requestJson = JsonSerializer.Serialize(request);
        var mockHttpRequest = CreateMockHttpRequest(requestJson);

        var mockDurableClient = new Mock<FakeDurableTaskClient>();
        mockDurableClient
            .Setup(c => c.ScheduleNewOrchestrationInstanceAsync("ApprovalOrchestration", request, null, default(CancellationToken)))
            .ReturnsAsync("test-instance-id");

        // Act
        var result = await _functions.StartApproval(mockHttpRequest.Object, mockDurableClient.Object);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        mockDurableClient.Verify(c => c.ScheduleNewOrchestrationInstanceAsync("ApprovalOrchestration", It.IsAny<ApprovalRequest>(), null, default(CancellationToken)), Times.Once);
    }

    [Fact]
    public async Task Reject_WithCompletedInstance_ShouldReturnAlreadyCompleted()
    {
        // Arrange
        var action = new ApprovalAction { InstanceId = "test-instance-id" };
        var requestJson = JsonSerializer.Serialize(action);
        var mockHttpRequest = CreateMockHttpRequest(requestJson);

        var mockDurableClient = new Mock<FakeDurableTaskClient>();
        mockDurableClient
            .Setup(c => c.GetInstanceAsync(action.InstanceId, false, default(CancellationToken)))
            .ReturnsAsync(new OrchestrationMetadata(Guid.NewGuid().ToString(), action.InstanceId)
            {
                RuntimeStatus = OrchestrationRuntimeStatus.Completed
            });

        // Act
        var result = await _functions.Reject(mockHttpRequest.Object, mockDurableClient.Object);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ResponseMessage>(okResult.Value);
        Assert.Equal("Orchestration Instance was already Completed.", response.Message);
        mockDurableClient.Verify(
            c => c.RaiseEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object?>(),
                default(CancellationToken)
            ),
            Times.Never
        );
    }



}