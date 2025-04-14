

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TaskApprovalBE.Infrastructure.Email
{
    public class EmailClientFactory(IServiceProvider serviceProvider) : IEmailClientFactory
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public IEmailClient CreateEmailClient(EmailServiceType emailServiceType)
        {
            var config = _serviceProvider.GetRequiredService<IConfiguration>();

            return emailServiceType switch
            {
                EmailServiceType.Azure => new AzureEmailClient(
                    config["AzureCommunication:ConnectionString"] ?? throw new ArgumentNullException("Azure:Email:ConnectionString"),
                    config["AzureCommunication:SenderAddress"] ?? throw new ArgumentNullException("Azure:Email:SenderAddress"),
                    _serviceProvider.GetRequiredService<ILogger<AzureEmailClient>>()),
                EmailServiceType.Mock => new MockEmailClient(),
                _ => throw new ArgumentOutOfRangeException(nameof(emailServiceType), emailServiceType, null)
            };
        }
    }
}
