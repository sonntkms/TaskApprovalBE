namespace TaskApprovalBE.Infrastructure.Email
{
    public interface IEmailClient
    {
        Task SendEmailAsync(string to, string subject, string body);
        string GetProviderInfo();
    }
}