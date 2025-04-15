namespace TaskApprovalBE.Services
{
    using System.Threading.Tasks;
    using Microsoft.DurableTask;
    using Microsoft.DurableTask.Client;
    using Microsoft.Extensions.Logging;
    using TaskApprovalBE.Infrastructure.Email;
    using TaskApprovalBE.Models;
    using TaskApprovalBE.Common.Constants;
    using Microsoft.Extensions.Configuration;

    public class ApprovalOrchestrationService(
        IEmailClient emailClient,
        IEmailTemplateService emailTemplateService,
        ILogger<ApprovalOrchestrationService> logger,
        IConfiguration config) : IApprovalOrchestrationService
    {
        private readonly IEmailClient _emailClient = emailClient;
        private readonly ILogger<ApprovalOrchestrationService> _logger = logger;
        private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;
        private readonly IConfiguration _config = config;

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

            if(!int.TryParse(_config[nameof(IApprovalOrchestrationService.DEFAULT_ORCHESTRATION_TIMEOUT)], out int duration)) {
                duration = IApprovalOrchestrationService.DEFAULT_ORCHESTRATION_TIMEOUT;
            }

            var timeout = context.CreateTimer(context.CurrentUtcDateTime.AddMonths(duration), CancellationToken.None); // after a certain period of time, the approval process will be automatically rejected.
            var approvalTask = context.WaitForExternalEvent<ApprovalResult>(IApprovalOrchestrationService.APPROVAL_EVENT_NAME);
            var winner = await Task.WhenAny(timeout, approvalTask);
            if (winner == timeout)
            {
                _logger.LogInformation($"Approval process timed out for user: {approvedRequest.UserEmail}. The approval request will be rejected automatically.");
                await context.CallActivityAsync(IApprovalOrchestrationService.REJECTED_NOTIFICATION_ACTIVITY_NAME, approvedRequest);
                return ApprovalOrchestrationResult.TIMED_OUT;
            } 
            else 
            {
               var result = await approvalTask;
                _logger.LogInformation($"Approval process completed for user: {approvedRequest.UserEmail}. The approval request will be {result.IsApproved}");
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

