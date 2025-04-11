namespace TaskApprovalBE.Models
{
    public class ApprovalResult
    {
        public bool IsApproved { get; set; }
    }

    public static class ApprovalOrchestrationResult
    {
        public const string APPROVED = "Approved";
        public const string REJECTED = "Rejected";
    }
}