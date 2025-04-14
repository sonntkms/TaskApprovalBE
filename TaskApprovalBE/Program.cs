using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaskApprovalBE.Infrastructure.Email;
using TaskApprovalBE.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.Services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddSingleton<IEmailClientFactory, EmailClientFactory>();
builder.Services.AddSingleton<IEmailClient>((sp) =>
{
    var emailClientFactory = sp.GetRequiredService<IEmailClientFactory>();
    return emailClientFactory.CreateEmailClient(EmailServiceType.Azure);
});
builder.Services.AddSingleton<IApprovalOrchestrationService, ApprovalOrchestrationService>();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
