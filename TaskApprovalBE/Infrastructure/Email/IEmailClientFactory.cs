namespace TaskApprovalBE.Infrastructure.Email
{
    public interface IEmailClientFactory
    {
        IEmailClient CreateEmailClient(EmailServiceType emailServiceType);
    }
}