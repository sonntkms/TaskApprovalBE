using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TaskApprovalBE.Infrastructure.Email;
using Xunit;

namespace TaskApprovalBE.Tests.Infrastructure.Email
{
    public class EmailClientFactoryTests
    {
        const string testConnectionString = "endpoint=https://test-resource-name.communication.azure.com/;accesskey=test-access-key";
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<AzureEmailClient>> _loggerMock;
        private readonly EmailClientFactory _factory;

        public EmailClientFactoryTests()
        {
            _serviceProviderMock = new Mock<IServiceProvider>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<AzureEmailClient>>();

            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IConfiguration)))
                .Returns(_configurationMock.Object);

            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(ILogger<AzureEmailClient>)))
                .Returns(_loggerMock.Object);

            _factory = new EmailClientFactory(_serviceProviderMock.Object);
        }

        [Fact]
        public void CreateEmailClient_ShouldReturnAzureEmailClient_WhenEmailServiceTypeIsAzure()
        {
            // Arrange
            _configurationMock
                .Setup(config => config["AzureCommunication:ConnectionString"])
                .Returns(testConnectionString);

            _configurationMock
                .Setup(config => config["AzureCommunication:SenderAddress"])
                .Returns("test@example.com");

            // Act
            var client = _factory.CreateEmailClient(EmailServiceType.Azure);

            // Assert
            Assert.NotNull(client);
            Assert.IsType<AzureEmailClient>(client);
        }

        [Fact]
        public void CreateEmailClient_ShouldThrowArgumentNullException_WhenAzureConnectionStringIsMissing()
        {
            // Arrange
            _ = _configurationMock
                .Setup(config => config["AzureCommunication:ConnectionString"])
                .Returns(null as string);

            _configurationMock
                .Setup(config => config["AzureCommunication:SenderAddress"])
                .Returns("test@example.com");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _factory.CreateEmailClient(EmailServiceType.Azure));
        }

        [Fact]
        public void CreateEmailClient_ShouldThrowArgumentNullException_WhenAzureSenderAddressIsMissing()
        {
            // Arrange
            _configurationMock
                .Setup(config => config["AzureCommunication:ConnectionString"])
                .Returns(testConnectionString);

            _configurationMock
                .Setup(config => config["AzureCommunication:SenderAddress"])
                .Returns(null as string);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _factory.CreateEmailClient(EmailServiceType.Azure));
        }

        [Fact]
        public void CreateEmailClient_ShouldReturnMockEmailClient_WhenEmailServiceTypeIsMock()
        {
            // Act
            var client = _factory.CreateEmailClient(EmailServiceType.Mock);

            // Assert
            Assert.NotNull(client);
            Assert.IsType<MockEmailClient>(client);
        }

        [Fact]
        public void CreateEmailClient_ShouldThrowArgumentOutOfRangeException_WhenEmailServiceTypeIsInvalid()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => _factory.CreateEmailClient((EmailServiceType)999));
        }
    }
}
