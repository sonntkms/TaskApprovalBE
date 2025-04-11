using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TaskApprovalBE.Services;
using TaskApprovalBE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.DurableTask.Client;
using System.Text.Json;

namespace TaskApprovalBE
{
    public class TaksApprovalFunctions(IEmailService emailService, ILogger<TaksApprovalFunctions> logger)
    {
        public const string APPROVAL_EVENT_NAME = "ApprovalEvent";
        private readonly IEmailService _emailService = emailService;
        private readonly ILogger<TaksApprovalFunctions> _logger = logger;

        [Function(nameof(SendStartedEmail))]
        public async Task SendStartedEmail([ActivityTrigger] ApprovalRequest request, FunctionContext functionContext)
        {
            _logger.LogInformation($"Sending 'process started' email to {request.UserEmail}");
            await _emailService.SendEmailAsync(
                request.UserEmail,
                $"Approval Process Started for {request.TaskName}",
                $"Your approval request has been started. Request ID: {request.Id}, Task Name: {request.TaskName}."
            );
        }

        [Function(nameof(SendApprovedEmail))]
        public async Task SendApprovedEmail([ActivityTrigger] ApprovalRequest request, FunctionContext functionContext)
        {
            _logger.LogInformation($"Sending 'approved' email to {request.UserEmail}");
            await _emailService.SendEmailAsync(
                request.UserEmail,
                $"Your Request Has Been Approved for {request.TaskName}",
                $"Congratulation! Your approval request (ID: {request.Id}, Task name: {request.TaskName}) has been approved.");
        }

        [Function(nameof(SendRejectedEmail))]
        public async Task SendRejectedEmail([ActivityTrigger] ApprovalRequest request, FunctionContext functionContext)
        {
            _logger.LogInformation($"Sending 'rejected' email to {request.UserEmail}");
            await _emailService.SendEmailAsync(
                request.UserEmail,
                $"Your Request Has Been Rejected for {request.TaskName}",
                $"We're sorry to inform you that your approval request (ID: {request.Id}, Task name: {request.TaskName}) has been rejected."
            );
        }

        [Function("ApprovalOrchestration")]
        public async Task<string> RunOrchestrationAsync(
            [OrchestrationTrigger] TaskOrchestrationContext context,
            ApprovalRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request), "ApprovalRequest cannot be null");
            }

            ApprovalRequest approvedRequest = request;

            _logger.LogInformation($"Starting approval process for user: {approvedRequest.UserEmail}");
            await context.CallActivityAsync(nameof(SendStartedEmail), request);

            ApprovalResult result = await context.WaitForExternalEvent<ApprovalResult>(APPROVAL_EVENT_NAME);
            if (result.IsApproved)
            {
                await context.CallActivityAsync(nameof(SendApprovedEmail), approvedRequest);
                return ApprovalOrchestrationResult.APPROVED;
            }
            else
            {
                await context.CallActivityAsync(nameof(SendRejectedEmail), approvedRequest);
                return ApprovalOrchestrationResult.REJECTED;
            }

        }

        [Function(nameof(StartApproval))]
        public async Task<IActionResult> StartApproval(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [DurableClient] DurableTaskClient starter)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<ApprovalRequest>(requestBody);

            if (string.IsNullOrEmpty(data?.UserEmail))
            {
                return new BadRequestObjectResult(new ResponseMessage("User email is required"));
            }

            // Start a new orchestration
            string instanceId = await starter.ScheduleNewOrchestrationInstanceAsync("ApprovalOrchestration", data);
            Console.WriteLine($"Started orchestration with ID = '{instanceId}'");
            _logger.LogInformation($"Started approval orchestration with ID = {instanceId}");

            return new OkObjectResult(new
            {
                InstanceId = instanceId,
                Message = "Approval process started successfully"
            });
        }

        private async Task<ActionResult?> ValidateApprovalActionRequest(ApprovalAction? data, DurableTaskClient client)
        {
            if (string.IsNullOrEmpty(data?.InstanceId))
            {
                return new BadRequestObjectResult(new ResponseMessage("Instance ID is required"));
            }
            var instance = await client.GetInstanceAsync(data.InstanceId);
            if (instance == null)
            {
                return new NotFoundObjectResult(new ResponseMessage($"No approval process found with ID: {data.InstanceId}"));
            }
            _logger.LogInformation($"Current status: {instance.RuntimeStatus}");
            if (instance.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
            {
                return new OkObjectResult(new ResponseMessage("Orchestration Instance was already Completed."));
            }

            return null;
        }

        [Function(nameof(Approve))]
        public async Task<IActionResult> Approve(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [DurableClient] DurableTaskClient client,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<ApprovalAction>(requestBody);

            var validationResult = await ValidateApprovalActionRequest(data, client);
            if (validationResult != null)
            {
                return validationResult;
            }

            // Raise the approval event
            await client.RaiseEventAsync(data.InstanceId, APPROVAL_EVENT_NAME, new ApprovalResult { IsApproved = true });

            _logger.LogInformation($"Approval request {data.InstanceId} has been approved");

            return new OkObjectResult(new ResponseMessage("Request approved successfully"));
        }

        [Function(nameof(Reject))]
        public async Task<IActionResult> Reject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [DurableClient] DurableTaskClient client)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<ApprovalAction>(requestBody);

            var validationResult = await ValidateApprovalActionRequest(data, client);
            if (validationResult != null)
            {
                return validationResult;
            }

            // Raise the rejection event
            await client.RaiseEventAsync(data.InstanceId, APPROVAL_EVENT_NAME, new ApprovalResult { IsApproved = false });

            _logger.LogInformation($"Approval request {data.InstanceId} has been rejected");

            return new OkObjectResult(new ResponseMessage("Request rejected successfully"));
        }
    }
}
