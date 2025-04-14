namespace TaskApprovalBE.Services
{
    using System.Threading.Tasks;
    using Microsoft.DurableTask;
    using Microsoft.DurableTask.Client;
    using Microsoft.Extensions.Logging;
    using TaskApprovalBE.Infrastructure.Email;
    using TaskApprovalBE.Models;
    using TaskApprovalBE.Common.Constants;

    public class ApprovalOrchestrationService(
        IEmailClient emailClient,
        IEmailTemplateService emailTemplateService,
        ILogger<ApprovalOrchestrationService> logger) : IApprovalOrchestrationService
    {
        private readonly IEmailClient _emailClient = emailClient;
        private readonly ILogger<ApprovalOrchestrationService> _logger = logger;
        private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;

        public Task NotifyApprovalCompletedAsync(ApprovalRequest request)
        {
            _logger.LogInformation($"Sending 'process completed' email to {request.UserEmail}");
            var emailContent = _emailTemplateService.GetApprovalCompletedEmailContent(new()
            {
                TaskName = request.TaskName,
                RequestId = request.Id
            });
            return _emailClient.SendEmailAsync(
                request.UserEmail,
                emailContent[0],
                emailContent[1]
            );
        }

        public Task NotifyApprovalRejectedAsync(ApprovalRequest request)
        {
            _logger.LogInformation($"Sending 'process rejected' email to {request.UserEmail}");
            var emailContent = _emailTemplateService.GetApprovalRejectedEmailContent(new()
            {
                TaskName = request.TaskName,
                RequestId = request.Id
            });
            return _emailClient.SendEmailAsync(
                request.UserEmail,
                emailContent[0],
                emailContent[1]
            );
        }

        public async Task NotifyApprovalStartedAsync(ApprovalRequest request)
        {
            _logger.LogInformation($"Sending 'process started' email to {request.UserEmail}");
            var emailContent = _emailTemplateService.GetApprovalStartEmailContent(new()
            {
                TaskName = request.TaskName,
                RequestId = request.Id
            });
            await _emailClient.SendEmailAsync(
                request.UserEmail,
               emailContent[0],
                emailContent[1]
            );
        }

        public async Task<string> RunOrchestrationAsync(TaskOrchestrationContext context, ApprovalRequest request)
        {
            ApprovalRequest approvedRequest = request;

            _logger.LogInformation($"Starting approval process for user: {approvedRequest.UserEmail}");
            await context.CallActivityAsync(IApprovalOrchestrationService.START_APPROVAL_NOTIFICATION_ACTIVITY_NAME, request);

            ApprovalResult result = await context.WaitForExternalEvent<ApprovalResult>(IApprovalOrchestrationService.APPROVAL_EVENT_NAME);
            if (result.IsApproved)
            {
                await context.CallActivityAsync(IApprovalOrchestrationService.APPROVED_NOTIFICATION_ACTIVITY_NAME, approvedRequest);
                return ApprovalOrchestrationResult.APPROVED;
            }
            else
            {
                await context.CallActivityAsync(IApprovalOrchestrationService.REJECTED_NOTIFICATION_ACTIVITY_NAME, approvedRequest);
                return ApprovalOrchestrationResult.REJECTED;
            }

        }

        private ResponseMessage? validateApprovalRequest(ApprovalRequest request)
        {
            if (string.IsNullOrEmpty(request.UserEmail))
            {
                return new ResponseMessage(Messages.RequiredUserEmail);
            }

            if (string.IsNullOrEmpty(request.TaskName))
            {
                return new ResponseMessage(Messages.RequiredTaskName);
            }

            return null;
        }

        public async Task<ResponseMessage> StartApprovalRequestAsync(DurableTaskClient client, ApprovalRequest request)
        {
            var validateResult = validateApprovalRequest(request);
            if (validateResult != null)
            {
                return validateResult;
            }

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(IApprovalOrchestrationService.APPROVAL_ORGESTRATION_NAME, request);
            _logger.LogInformation($"Started approval orchestration with ID = {instanceId}");
            return new ResponseMessage(Messages.ApprovalProcessStarted, instanceId);
        }

        private async Task<ResponseMessage?> ValidateApprovalActionRequest(ApprovalActionRequest? data, DurableTaskClient client)
        {
            if (string.IsNullOrEmpty(data?.InstanceId))
            {
                return new ResponseMessage(Messages.RequiredInstanceId);
            }
            var instance = await client.GetInstanceAsync(data.InstanceId);
            if (instance == null)
            {
                return new ResponseMessage(string.Format(Messages.NoApprovalProcessFound, data.InstanceId));
            }

            _logger.LogInformation($"Current status: {instance.RuntimeStatus}");
            if (instance.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
            {
                return new ResponseMessage(Messages.CompletedOrchestrationInstance);
            }

            return null;
        }

        public async Task<ResponseMessage> PerformApprovalActionAsync(DurableTaskClient client, ApprovalActionRequest request, bool isApproved)
        {
            var validationResult = await ValidateApprovalActionRequest(request, client);
            if (validationResult != null)
            {
                return validationResult;
            }

            await client.RaiseEventAsync(request.InstanceId!, IApprovalOrchestrationService.APPROVAL_EVENT_NAME, new ApprovalResult { IsApproved = isApproved });

            _logger.LogInformation($"Approval request {request.InstanceId} has been approved");

            return new ResponseMessage(
                isApproved ? Messages.RequestApproveSuccess : Messages.RequestRejectSuccess,
                instanceId: request.InstanceId);
        }
    }
}

