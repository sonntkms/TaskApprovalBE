using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using TaskApprovalBE.Services;
using TaskApprovalBE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.DurableTask.Client;
using System.Text.Json;

namespace TaskApprovalBE
{
    public class TaksApprovalFunctions(
        IApprovalOrchestrationService approvalOrchestrationService
    )
    {
        private readonly IApprovalOrchestrationService _approvalOrchestrationService = approvalOrchestrationService;

        [Function(IApprovalOrchestrationService.START_APPROVAL_NOTIFICATION_ACTIVITY_NAME)]
        public async Task SendStartedEmail([ActivityTrigger] ApprovalRequest request, FunctionContext functionContext)
        {
            await _approvalOrchestrationService.NotifyApprovalStartedAsync(request);
        }

        [Function(IApprovalOrchestrationService.APPROVED_NOTIFICATION_ACTIVITY_NAME)]
        public async Task SendApprovedEmail([ActivityTrigger] ApprovalRequest request, FunctionContext functionContext)
        {
            await _approvalOrchestrationService.NotifyApprovalCompletedAsync(request);
        }

        [Function(IApprovalOrchestrationService.REJECTED_NOTIFICATION_ACTIVITY_NAME)]
        public async Task SendRejectedEmail([ActivityTrigger] ApprovalRequest request, FunctionContext functionContext)
        {
            await _approvalOrchestrationService.NotifyApprovalRejectedAsync(request);
        }

        [Function(IApprovalOrchestrationService.APPROVAL_ORGESTRATION_NAME)]
        public async Task<string> RunOrchestrationAsync(
            [OrchestrationTrigger] TaskOrchestrationContext context,
            ApprovalRequest request)
        {
            return await _approvalOrchestrationService.RunOrchestrationAsync(context, request);
        }

        private IActionResult handleResponseMessage(ResponseMessage result)
        {
            if (string.IsNullOrEmpty(result.InstanceId))
            {
                return new BadRequestObjectResult(result);
            }

            return new OkObjectResult(result);
        }

        [Function(IApprovalOrchestrationService.START_APPROVAL_TRIGGER_NAME)]
        public async Task<IActionResult> StartApproval(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [DurableClient] DurableTaskClient starter)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<ApprovalRequest>(requestBody);

            var result = await _approvalOrchestrationService.StartApprovalRequestAsync(starter, data!);
            return handleResponseMessage(result);
        }

        [Function(IApprovalOrchestrationService.APPROVE_TRIGGER_NAME)]
        public async Task<IActionResult> Approve(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [DurableClient] DurableTaskClient client)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<ApprovalActionRequest>(requestBody);

            var result = await _approvalOrchestrationService.PerformApprovalActionAsync(client, data!, true);
            return handleResponseMessage(result);
        }

        [Function(IApprovalOrchestrationService.REJECT_TRIGGER_NAME)]
        public async Task<IActionResult> Reject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [DurableClient] DurableTaskClient client)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<ApprovalActionRequest>(requestBody);

            var result = await _approvalOrchestrationService.PerformApprovalActionAsync(client, data!, false);
            return handleResponseMessage(result);
        }
    }
}
