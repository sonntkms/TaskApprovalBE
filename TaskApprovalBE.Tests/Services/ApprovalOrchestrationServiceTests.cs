using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Moq;
using TaskApprovalBE.Infrastructure.Email;
using TaskApprovalBE.Models;
using TaskApprovalBE.Services;
using TaskApprovalBE.Tests.Helpers;

namespace TaskApprovalBE.Tests.Services
{
    public class ApprovalOrchestrationServiceTests
    {
        private readonly Mock<IEmailClient> _mockEmailClient;
        private readonly Mock<IEmailTemplateService> _mockEmailTemplateService;
        private readonly Mock<ILogger<ApprovalOrchestrationService>> _mockLogger;
        private readonly ApprovalOrchestrationService _service;

        public ApprovalOrchestrationServiceTests()
        {
            _mockEmailClient = new Mock<IEmailClient>();
            _mockEmailTemplateService = new Mock<IEmailTemplateService>();
            _mockLogger = new Mock<ILogger<ApprovalOrchestrationService>>();

            _service = new ApprovalOrchestrationService(
                _mockEmailClient.Object,
                _mockEmailTemplateService.Object,
                _mockLogger.Object
            );
        }


        [Fact]
        public async Task NotifyApprovalStartedAsync_SendsCorrectEmail()
        {
            // Arrange
            var request = new ApprovalRequest
            {
                Id = "req-123",
                UserEmail = "user@example.com",
                TaskName = "Test Task"
            };

            var emailContent = new[] { "Approval Subject", "Approval Body" };
            _mockEmailTemplateService
                .Setup(s => s.GetApprovalStartEmailContent(It.IsAny<NotificationEmailData>()))
                .Returns(emailContent);

            // Act
            await _service.NotifyApprovalStartedAsync(request);

            // Assert
            _mockEmailTemplateService.Verify(s =>
                s.GetApprovalStartEmailContent(It.Is<NotificationEmailData>(d =>
                    d.TaskName == request.TaskName &&
                    d.RequestId == request.Id)),
                Times.Once);

            _mockEmailClient.Verify(c =>
                c.SendEmailAsync(
                    request.UserEmail,
                    emailContent[0],
                    emailContent[1]),
                Times.Once);
        }

        [Fact]
        public async Task NotifyApprovalCompletedAsync_SendsCorrectEmail()
        {
            // Arrange
            var request = new ApprovalRequest
            {
                Id = "req-123",
                UserEmail = "user@example.com",
                TaskName = "Test Task"
            };

            var emailContent = new[] { "Completed Subject", "Completed Body" };
            _mockEmailTemplateService
                .Setup(s => s.GetApprovalCompletedEmailContent(It.IsAny<NotificationEmailData>()))
                .Returns(emailContent);

            // Act
            await _service.NotifyApprovalCompletedAsync(request);

            // Assert
            _mockEmailTemplateService.Verify(s =>
                s.GetApprovalCompletedEmailContent(It.Is<NotificationEmailData>(d =>
                    d.TaskName == request.TaskName &&
                    d.RequestId == request.Id)),
                Times.Once);

            _mockEmailClient.Verify(c =>
                c.SendEmailAsync(
                    request.UserEmail,
                    emailContent[0],
                    emailContent[1]),
                Times.Once);
        }

        [Fact]
        public async Task NotifyApprovalRejectedAsync_SendsCorrectEmail()
        {
            // Arrange
            var request = new ApprovalRequest
            {
                Id = "req-123",
                UserEmail = "user@example.com",
                TaskName = "Test Task"
            };

            var emailContent = new[] { "Rejected Subject", "Rejected Body" };
            _mockEmailTemplateService
                .Setup(s => s.GetApprovalRejectedEmailContent(It.IsAny<NotificationEmailData>()))
                .Returns(emailContent);

            // Act
            await _service.NotifyApprovalRejectedAsync(request);

            // Assert
            _mockEmailTemplateService.Verify(s =>
                s.GetApprovalRejectedEmailContent(It.Is<NotificationEmailData>(d =>
                    d.TaskName == request.TaskName &&
                    d.RequestId == request.Id)),
                Times.Once);

            _mockEmailClient.Verify(c =>
                c.SendEmailAsync(
                    request.UserEmail,
                    emailContent[0],
                    emailContent[1]),
                Times.Once);
        }


        [Fact]
        public async Task RunOrchestrationAsync_WhenApproved_CallsApprovedNotification()
        {
            // Arrange
            var request = new ApprovalRequest
            {
                Id = "req-123",
                UserEmail = "user@example.com",
                TaskName = "Test Task"
            };

            var mockContext = new Mock<TaskOrchestrationContext>();

            // Set up the WaitForExternalEvent to return an approved result
            mockContext
                .Setup(c => c.WaitForExternalEvent<ApprovalResult>(IApprovalOrchestrationService.APPROVAL_EVENT_NAME, default(CancellationToken)))
                .ReturnsAsync(new ApprovalResult { IsApproved = true });

            // Act
            var result = await _service.RunOrchestrationAsync(mockContext.Object, request);

            // Assert
            Assert.Equal(ApprovalOrchestrationResult.APPROVED, result);

            mockContext.Verify(c =>
                c.CallActivityAsync(
                    IApprovalOrchestrationService.START_APPROVAL_NOTIFICATION_ACTIVITY_NAME,
                    request, null),
                Times.Once);

            mockContext.Verify(c =>
                c.CallActivityAsync(
                    IApprovalOrchestrationService.APPROVED_NOTIFICATION_ACTIVITY_NAME,
                    request, null),
                Times.Once);

            mockContext.Verify(c =>
                c.CallActivityAsync(
                    IApprovalOrchestrationService.REJECTED_NOTIFICATION_ACTIVITY_NAME,
                    It.IsAny<ApprovalRequest>(), null),
                Times.Never);
        }

        [Fact]
        public async Task RunOrchestrationAsync_WhenRejected_CallsRejectedNotification()
        {
            // Arrange
            var request = new ApprovalRequest
            {
                Id = "req-123",
                UserEmail = "user@example.com",
                TaskName = "Test Task"
            };

            var mockContext = new Mock<TaskOrchestrationContext>();

            // Set up the WaitForExternalEvent to return a rejected result
            mockContext
                .Setup(c => c.WaitForExternalEvent<ApprovalResult>(IApprovalOrchestrationService.APPROVAL_EVENT_NAME, default(CancellationToken)))
                .ReturnsAsync(new ApprovalResult { IsApproved = false });

            // Act
            var result = await _service.RunOrchestrationAsync(mockContext.Object, request);

            // Assert
            Assert.Equal(ApprovalOrchestrationResult.REJECTED, result);

            mockContext.Verify(c =>
                c.CallActivityAsync(
                    IApprovalOrchestrationService.START_APPROVAL_NOTIFICATION_ACTIVITY_NAME,
                    request, null),
                Times.Once);

            mockContext.Verify(c =>
                c.CallActivityAsync(
                    IApprovalOrchestrationService.REJECTED_NOTIFICATION_ACTIVITY_NAME,
                    request, null),
                Times.Once);

            mockContext.Verify(c =>
                c.CallActivityAsync(
                    IApprovalOrchestrationService.APPROVED_NOTIFICATION_ACTIVITY_NAME,
                    It.IsAny<ApprovalRequest>(), null),
                Times.Never);
        }

        [Fact]
        public async Task StartApprovalRequestAsync_WithValidRequest_StartsOrchestration()
        {
            // Arrange
            var request = new ApprovalRequest
            {
                Id = "req-123",
                UserEmail = "user@example.com",
                TaskName = "Test Task"
            };

            var mockClient = new Mock<FakeDurableTaskClient>();
            const string expectedInstanceId = "instance-123";
            mockClient
                .Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
                    IApprovalOrchestrationService.APPROVAL_ORGESTRATION_NAME,
                    It.IsAny<ApprovalRequest>(), null, default(CancellationToken)))
                .ReturnsAsync(expectedInstanceId);

            // Act
            var result = await _service.StartApprovalRequestAsync(mockClient.Object, request);

            // Assert
            Assert.Equal(expectedInstanceId, result.InstanceId);
            Assert.Contains("started successfully", result.Message);

            mockClient.Verify(c =>
                c.ScheduleNewOrchestrationInstanceAsync(
                    IApprovalOrchestrationService.APPROVAL_ORGESTRATION_NAME,
                    request, null, default(CancellationToken)),
                Times.Once);
        }

        [Fact]
        public async Task StartApprovalRequestAsync_WithMissingEmail_ReturnsError()
        {
            // Arrange
            var request = new ApprovalRequest
            {
                Id = "req-123",
                TaskName = "Test Task",
                UserEmail = "" // Empty email
            };

            var mockClient = new Mock<FakeDurableTaskClient>();

            // Act
            var result = await _service.StartApprovalRequestAsync(mockClient.Object, request);

            // Assert
            Assert.Null(result.InstanceId);
            Assert.Contains("email is required", result.Message);
        }

        [Fact]
        public async Task StartApprovalRequestAsync_WithMissingTaskName_ReturnsError()
        {
            // Arrange
            var request = new ApprovalRequest
            {
                Id = "req-123",
                UserEmail = "user@example.com",
                TaskName = "" // Empty task name
            };

            var mockClient = new Mock<FakeDurableTaskClient>();

            // Act
            var result = await _service.StartApprovalRequestAsync(mockClient.Object, request);

            // Assert
            Assert.Null(result.InstanceId);
            Assert.Contains("Task name is required", result.Message);

            mockClient.Verify(c =>
                c.ScheduleNewOrchestrationInstanceAsync(
                    IApprovalOrchestrationService.APPROVAL_ORGESTRATION_NAME,
                    It.IsAny<ApprovalRequest>(),
                    It.IsAny<StartOrchestrationOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task PerformApprovalActionAsync_WithValidRequest_RaisesEvent()
        {
            // Arrange
            var request = new ApprovalActionRequest { InstanceId = "instance-123" };

            var mockClient = new Mock<FakeDurableTaskClient>();

            mockClient
                .Setup(c => c.GetInstanceAsync(request.InstanceId, false, default(CancellationToken)))
                .ReturnsAsync(new OrchestrationMetadata(Guid.NewGuid().ToString(), "instance-123")
                {
                    RuntimeStatus = OrchestrationRuntimeStatus.Running
                });

            // Act
            var result = await _service.PerformApprovalActionAsync(mockClient.Object, request, true);

            // Assert
            Assert.Equal(request.InstanceId, result.InstanceId);
            Assert.Contains("approved successfully", result.Message);

            mockClient.Verify(c =>
                c.RaiseEventAsync(
                    request.InstanceId,
                    IApprovalOrchestrationService.APPROVAL_EVENT_NAME,
                    It.Is<ApprovalResult>(r => r.IsApproved),
                    default(CancellationToken)),
                Times.Once);
        }

        [Fact]
        public async Task PerformApprovalActionAsync_WithRejection_RaisesRejectEvent()
        {
            // Arrange
            var request = new ApprovalActionRequest { InstanceId = "instance-123" };

            var mockClient = new Mock<FakeDurableTaskClient>();

            mockClient
                 .Setup(c => c.GetInstanceAsync(request.InstanceId, false, default(CancellationToken)))
                .ReturnsAsync(new OrchestrationMetadata(Guid.NewGuid().ToString(), request.InstanceId)
                {
                    RuntimeStatus = OrchestrationRuntimeStatus.Running
                });

            // Act
            var result = await _service.PerformApprovalActionAsync(mockClient.Object, request, false);

            // Assert
            Assert.Equal(request.InstanceId, result.InstanceId);
            Assert.Contains("rejected successfully", result.Message);

            mockClient.Verify(c =>
                c.RaiseEventAsync(
                    request.InstanceId,
                    IApprovalOrchestrationService.APPROVAL_EVENT_NAME,
                    It.Is<ApprovalResult>(r => !r.IsApproved),
                    default(CancellationToken)),
                Times.Once);
        }

        [Fact]
        public async Task PerformApprovalActionAsync_WithMissingInstanceId_ReturnsError()
        {
            // Arrange
            var request = new ApprovalActionRequest { InstanceId = "" }; // Empty instance ID
            var mockClient = new Mock<FakeDurableTaskClient>();

            // Act
            var result = await _service.PerformApprovalActionAsync(mockClient.Object, request, true);

            // Assert
            Assert.Null(result.InstanceId);
            Assert.Contains("Instance ID is required", result.Message);

            mockClient.Verify(c =>
                c.RaiseEventAsync(
                    request.InstanceId,
                    IApprovalOrchestrationService.APPROVAL_EVENT_NAME,
                    It.IsAny<ApprovalResult>(),
                    default(CancellationToken)),
                Times.Never);
        }

        [Fact]
        public async Task PerformApprovalActionAsync_WithNonExistingInstance_ReturnsError()
        {
            // Arrange
            var request = new ApprovalActionRequest { InstanceId = "non-existing" };

            var mockClient = new Mock<FakeDurableTaskClient>();
            mockClient
                .Setup(c => c.GetInstanceAsync(request.InstanceId, false, default(CancellationToken)))
                .ReturnsAsync(null as OrchestrationMetadata);

            // Act
            var result = await _service.PerformApprovalActionAsync(mockClient.Object, request, true);

            // Assert
            Assert.Null(result.InstanceId);
            Assert.Contains("No approval process found", result.Message);

            mockClient.Verify(c =>
                c.RaiseEventAsync(
                    request.InstanceId,
                    IApprovalOrchestrationService.APPROVAL_EVENT_NAME,
                    It.IsAny<ApprovalResult>(),
                    default(CancellationToken)),
                Times.Never);
        }

        [Fact]
        public async Task PerformApprovalActionAsync_WithCompletedInstance_ReturnsError()
        {
            // Arrange
            var request = new ApprovalActionRequest { InstanceId = "completed-instance" };

            var mockClient = new Mock<FakeDurableTaskClient>();

            mockClient
                .Setup(c => c.GetInstanceAsync(request.InstanceId, false, default(CancellationToken)))
                .ReturnsAsync(new OrchestrationMetadata(Guid.NewGuid().ToString(), request.InstanceId)
                {
                    RuntimeStatus = OrchestrationRuntimeStatus.Completed
                });

            // Act
            var result = await _service.PerformApprovalActionAsync(mockClient.Object, request, true);

            // Assert
            Assert.Null(result.InstanceId);
            Assert.Contains("already Completed", result.Message);
        }
    }
}