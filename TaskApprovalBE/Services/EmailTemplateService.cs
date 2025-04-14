using HandlebarsDotNet;
using TaskApprovalBE.Models;

namespace TaskApprovalBE.Services
{
    public class EmailTemplateService : IEmailTemplateService
    {
        private const string ApprovedSubjectTemplatePath = "Templates/Emails/ApprovedRequestNotificationSubject.txt";
        private const string ApprovedBodyTemplatePath = "Templates/Emails/ApprovedRequestNotification.html";
        private const string RejectedSubjectTemplatePath = "Templates/Emails/RejectedRequestNotificationSubject.txt";
        private const string RejectedBodyTemplatePath = "Templates/Emails/RejectedRequestNotification.html";
        private const string ApprovalStartSubjectTemplatePath = "Templates/Emails/ApprovalStartNotificationSubject.txt";
        private const string ApprovalStartBodyTemplatePath = "Templates/Emails/ApprovalStartNotification.html";

        public string[] GetApprovalCompletedEmailContent(NotificationEmailData emailData)
        {
            return
            [
                RenderTemplate(ApprovedSubjectTemplatePath, emailData),
                RenderTemplate(ApprovedBodyTemplatePath, emailData)
            ];
        }

        public string[] GetApprovalRejectedEmailContent(NotificationEmailData emailData)
        {
            return
            [
                RenderTemplate(RejectedSubjectTemplatePath, emailData),
                RenderTemplate(RejectedBodyTemplatePath, emailData)
            ];
        }

        public string[] GetApprovalStartEmailContent(NotificationEmailData emailData)
        {
            return
            [
                RenderTemplate(ApprovalStartSubjectTemplatePath, emailData),
                RenderTemplate(ApprovalStartBodyTemplatePath, emailData)
            ];
        }

        public string RenderTemplate(string templatePath, object model)
        {
            var templateContent = File.ReadAllText(templatePath);
            var template = Handlebars.Compile(templateContent);
            return template(model);
        }


    }
}