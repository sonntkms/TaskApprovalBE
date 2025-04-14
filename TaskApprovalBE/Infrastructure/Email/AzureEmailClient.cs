using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.Communication.Email;

namespace TaskApprovalBE.Infrastructure.Email
{
    public class AzureEmailClient(string connectionString, string senderAddress, ILogger<AzureEmailClient> logger) : IEmailClient
    {
        private readonly ILogger<AzureEmailClient> _logger = logger;
        private readonly string _emailSender = senderAddress;
        private readonly EmailClient _client = new(connectionString);

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var content = new EmailContent(subject)
                {
                    Html = body
                };
                var mailMessage = new EmailMessage(
                     senderAddress: _emailSender,
                     content: content,
                     recipientAddress: to
                );

                _logger.LogInformation($"Sending email to {to} with subject '{subject}'");
                var emailSendOperation = await _client.SendAsync(Azure.WaitUntil.Started, mailMessage);
                await emailSendOperation.WaitForCompletionAsync();
                _logger.LogInformation($"Email send successfully. Operation ID: {emailSendOperation.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {to}. Error: {ex.Message}");
                throw;
            }
        }

        public string GetProviderInfo()
        {
            return "Azure Email Client";
        }
    }
}