using Microsoft.DurableTask.Client;

namespace TaskApprovalBE.Services;

public interface IDurableTaskClient
{
    Task<string> ScheduleNewOrchestrationInstanceAsync(string orchestratorFunctionName, object input);
    Task<OrchestrationMetadata?> GetInstanceAsync(string instanceId);
    Task RaiseEventAsync(string instanceId, string eventName, object eventData);
}