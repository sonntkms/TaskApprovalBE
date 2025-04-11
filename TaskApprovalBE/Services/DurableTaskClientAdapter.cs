using Microsoft.DurableTask.Client;

namespace TaskApprovalBE.Services;

public class DurableTaskClientAdapter : IDurableTaskClient
{
    private readonly DurableTaskClient _client;

    public DurableTaskClientAdapter(DurableTaskClient client)
    {
        _client = client;
    }

    public Task<string> ScheduleNewOrchestrationInstanceAsync(string orchestratorFunctionName, object input)
    {
        return _client.ScheduleNewOrchestrationInstanceAsync(orchestratorFunctionName, input);
    }

    public  Task<OrchestrationMetadata?> GetInstanceAsync(string instanceId)
    {
        return _client.GetInstanceAsync(instanceId);
    }

    public Task RaiseEventAsync(string instanceId, string eventName, object eventData)
    {
        return _client.RaiseEventAsync(instanceId, eventName, eventData);
    }
}