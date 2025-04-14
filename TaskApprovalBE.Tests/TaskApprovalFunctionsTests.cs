using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Moq;
using TaskApprovalBE.Models;
using TaskApprovalBE.Services;
using TaskApprovalBE.Tests.Helpers;

namespace TaskApprovalBE.Tests
{
    public class TaksApprovalFunctionsTests
    {
        private readonly Mock<IApprovalOrchestrationService> _mockService;
        private readonly TaksApprovalFunctions _functions;

        public TaksApprovalFunctionsTests()
        {
            _mockService = new Mock<IApprovalOrchestrationService>();
            _functions = new TaksApprovalFunctions(_mockService.Object);
        }

        [Fact]
        public async Task SendStartedEmail_CallsNotifyApprovalStartedAsync()
        {
            // Arrange
            var request = new ApprovalRequest { Id = "req-123", TaskName = "Test Task" };
            var functionContext = new Mock<FunctionContext>().Object;

            // Act
            await _functions.SendStartedEmail(request, functionContext);

            // Assert
            _mockService.Verify(s => s.NotifyApprovalStartedAsync(request), Times.Once);
        }

        [Fact]
        public async Task SendApprovedEmail_CallsNotifyApprovalCompletedAsync()
        {
            // Arrange
            var request = new ApprovalRequest { Id = "req-123", TaskName = "Test Task" };
            var functionContext = new Mock<FunctionContext>().Object;

            // Act
            await _functions.SendApprovedEmail(request, functionContext);

            // Assert
            _mockService.Verify(s => s.NotifyApprovalCompletedAsync(request), Times.Once);
        }

        [Fact]
        public async Task SendRejectedEmail_CallsNotifyApprovalRejectedAsync()
        {
            // Arrange
            var request = new ApprovalRequest { Id = "req-123", TaskName = "Test Task" };
            var functionContext = new Mock<FunctionContext>().Object;

            // Act
            await _functions.SendRejectedEmail(request, functionContext);

            // Assert
            _mockService.Verify(s => s.NotifyApprovalRejectedAsync(request), Times.Once);
        }

        [Fact]
        public async Task RunOrchestrationAsync_CallsServiceWithContext()
        {
            // Arrange
            var request = new ApprovalRequest { Id = "req-123", TaskName = "Test Task" };
            var mockContext = new Mock<TaskOrchestrationContext>();
            _mockService.Setup(s => s.RunOrchestrationAsync(It.IsAny<TaskOrchestrationContext>(), It.IsAny<ApprovalRequest>()))
                .ReturnsAsync("orchestration-123");

            // Act
            var result = await _functions.RunOrchestrationAsync(mockContext.Object, request);

            // Assert
            Assert.Equal("orchestration-123", result);
            _mockService.Verify(s => s.RunOrchestrationAsync(mockContext.Object, request), Times.Once);
        }

        private HttpRequest CreateMockHttpRequest(string jsonContent)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
            var mockRequest = new Mock<HttpRequest>();
            mockRequest.Setup(r => r.Body).Returns(stream);
            return mockRequest.Object;
        }

        [Fact]
        public async Task StartApproval_ReturnsOkResult_WhenSuccessful()
        {
            // Arrange
            var request = new ApprovalRequest { Id = "req-123", TaskName = "Test Task" };
            var jsonContent = JsonSerializer.Serialize(request);
            var mockRequest = CreateMockHttpRequest(jsonContent);

            var mockClient = new Mock<FakeDurableTaskClient>();

            var expectedResponse = new ResponseMessage("instance-123", "Success");
            _mockService.Setup(s => s.StartApprovalRequestAsync(It.IsAny<DurableTaskClient>(), It.IsAny<ApprovalRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _functions.StartApproval(mockRequest, mockClient.Object);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var responseValue = Assert.IsType<ResponseMessage>(okResult.Value);
            Assert.Equal(expectedResponse.InstanceId, responseValue.InstanceId);
            Assert.Equal(expectedResponse.Message, responseValue.Message);
        }

        [Fact]
        public async Task StartApproval_ReturnsBadRequest_WhenInstanceIdIsEmpty()
        {
            // Arrange
            var request = new ApprovalRequest { Id = "req-123", TaskName = "Test Task" };
            var jsonContent = JsonSerializer.Serialize(request);
            var mockRequest = CreateMockHttpRequest(jsonContent);

            var mockClient = new Mock<FakeDurableTaskClient>();

            var expectedResponse = new ResponseMessage("Error");
            _mockService.Setup(s => s.StartApprovalRequestAsync(It.IsAny<DurableTaskClient>(), It.IsAny<ApprovalRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _functions.StartApproval(mockRequest, mockClient.Object);

            // Assert
            var badResult = Assert.IsType<BadRequestObjectResult>(result);
            var responseValue = Assert.IsType<ResponseMessage>(badResult.Value);
            Assert.Equal(expectedResponse.Message, responseValue.Message);
        }

        [Fact]
        public async Task Approve_ReturnsOkResult_WhenSuccessful()
        {
            // Arrange
            var request = new ApprovalActionRequest { InstanceId = "instance-123" };
            var jsonContent = JsonSerializer.Serialize(request);
            var mockRequest = CreateMockHttpRequest(jsonContent);
            var mockClient = new Mock<FakeDurableTaskClient>();

            var expectedResponse = new ResponseMessage("instance-123", "Success");
            _mockService.Setup(s => s.PerformApprovalActionAsync(It.IsAny<DurableTaskClient>(), It.IsAny<ApprovalActionRequest>(), true))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _functions.Approve(mockRequest, mockClient.Object);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var responseValue = Assert.IsType<ResponseMessage>(okResult.Value);
            Assert.Equal(expectedResponse.InstanceId, responseValue.InstanceId);
            Assert.Equal(expectedResponse.Message, responseValue.Message);
        }

        [Fact]
        public async Task Reject_ReturnsOkResult_WhenSuccessful()
        {
            // Arrange
            var request = new ApprovalActionRequest { InstanceId = "instance-123" };
            var jsonContent = JsonSerializer.Serialize(request);
            var mockRequest = CreateMockHttpRequest(jsonContent);

            var mockClient = new Mock<FakeDurableTaskClient>();

            var expectedResponse = new ResponseMessage("instance-123", "Rejected Successfullly");
            _mockService.Setup(s => s.PerformApprovalActionAsync(It.IsAny<DurableTaskClient>(), It.IsAny<ApprovalActionRequest>(), false))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _functions.Reject(mockRequest, mockClient.Object);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var responseValue = Assert.IsType<ResponseMessage>(okResult.Value);
            Assert.Equal(expectedResponse.InstanceId, responseValue.InstanceId);
            Assert.Equal(expectedResponse.Message, responseValue.Message);
        }
    }
}