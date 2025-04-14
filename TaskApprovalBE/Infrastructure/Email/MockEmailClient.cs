namespace TaskApprovalBE.Infrastructure.Email
{
    public class MockEmailClient : IEmailClient
    {
        public Task SendEmailAsync(string to, string subject, string body)
        {
            // Simulate sending an email
            return Task.CompletedTask;
        }

        public string GetProviderInfo()
        {
            return "Mock Email Provider";
        }
    }
}
