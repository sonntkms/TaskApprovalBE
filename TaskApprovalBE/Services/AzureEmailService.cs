using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.Communication.Email;

namespace TaskApprovalBE.Services
{
    public class AzureEmailService(ILogger<AzureEmailService> logger, IConfiguration configuration) : IEmailService
    {
        private readonly ILogger<AzureEmailService> _logger = logger;
        private readonly string _emailSender = configuration["AzureCommunication:SenderAddress"] ?? throw new ArgumentNullException("Email sender cannot be null");
        private readonly EmailClient _client = !string.IsNullOrEmpty(configuration["AzureCommunication:ConnectionString"]) ?
            new EmailClient(configuration["AzureCommunication:ConnectionString"]) :
            throw new ArgumentNullException("Connection string cannot be null");

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var content = new EmailContent(subject)
                {
                    PlainText = body,
                    Html = $"<html><body>{body}</body></html>"
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
    }
}