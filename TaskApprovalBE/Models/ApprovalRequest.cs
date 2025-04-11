
namespace TaskApprovalBE.Models;
public class ApprovalRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserEmail { get; set; }
    public required string TaskName { get; set; }
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;
}