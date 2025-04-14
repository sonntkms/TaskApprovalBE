
namespace TaskApprovalBE.Models;
public class ApprovalRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserEmail { get; set; }
    public string TaskName { get; set; }
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;
}