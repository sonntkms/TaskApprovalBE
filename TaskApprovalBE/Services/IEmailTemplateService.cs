using TaskApprovalBE.Models;

namespace TaskApprovalBE.Services
{
    public interface IEmailTemplateService
    {
        string[] GetApprovalStartEmailContent(NotificationEmailData emailData);
        string[] GetApprovalCompletedEmailContent(NotificationEmailData emailData);
        string[] GetApprovalRejectedEmailContent(NotificationEmailData emailData);
    }
}
