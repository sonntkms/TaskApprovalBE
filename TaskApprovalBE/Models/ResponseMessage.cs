using Azure;

namespace TaskApprovalBE.Models;

public class ResponseMessage(string message, string? instanceId = null)
{
    public string? Message { get; set; } = message;
    public string? InstanceId { get; set; } = instanceId;
}
