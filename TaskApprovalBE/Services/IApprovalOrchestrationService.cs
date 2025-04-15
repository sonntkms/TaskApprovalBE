using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using TaskApprovalBE.Models;

namespace TaskApprovalBE.Services
{
    public interface IApprovalOrchestrationService
    {
        const string APPROVAL_EVENT_NAME = "ApprovalEvent";
        const string START_APPROVAL_NOTIFICATION_ACTIVITY_NAME = "StartApprovalNotification";
        const string APPROVED_NOTIFICATION_ACTIVITY_NAME = "ApprovedNotification";
        const string REJECTED_NOTIFICATION_ACTIVITY_NAME = "RejectedNotification";
        const string APPROVAL_ORGESTRATION_NAME = "ApprovalOrchestration";
        const string START_APPROVAL_TRIGGER_NAME = "StartApproval";
        const string APPROVE_TRIGGER_NAME = "Approve";
        const string REJECT_TRIGGER_NAME = "Reject";
        const int DEFAULT_ORCHESTRATION_TIMEOUT = 6; // 6 months

        Task NotifyApprovalStartedAsync(ApprovalRequest request);
        Task NotifyApprovalCompletedAsync(ApprovalRequest request);
        Task NotifyApprovalRejectedAsync(ApprovalRequest request);

        Task<string> RunOrchestrationAsync( TaskOrchestrationContext context, ApprovalRequest request);

        Task<ResponseMessage> StartApprovalRequestAsync( DurableTaskClient client, ApprovalRequest request);

        Task<ResponseMessage> PerformApprovalActionAsync( DurableTaskClient client, ApprovalActionRequest request, bool isApproved);   

    }
}
