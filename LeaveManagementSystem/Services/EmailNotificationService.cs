using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LeaveManagementSystem.Services
{
    public class SmtpSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public string FromName { get; set; } = "LMS";
    }

    public interface IEmailNotificationService
    {
        Task SendAsync(string toEmail, string subject, string htmlBody);

        Task SendAsync(List<string> toEmails, string subject, string htmlBody);
    }

    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly SmtpSettings _smtp;

        public EmailNotificationService(IOptions<SmtpSettings> smtpOptions)
        {
            _smtp = smtpOptions.Value;
        }

        // ✅ 原本单收件人：继续保留（内部转给多收件人）
        public Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            return SendAsync(new List<string> { toEmail }, subject, htmlBody);
        }

        // ✅ NEW: 多收件人
        public async Task SendAsync(List<string> toEmails, string subject, string htmlBody)
        {
            if (toEmails == null || toEmails.Count == 0)
                return;

            // 清理/去重/过滤空值
            var emails = toEmails
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (emails.Count == 0)
                return;

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));

            foreach (var email in emails)
            {
                msg.To.Add(MailboxAddress.Parse(email));
            }

            msg.Subject = subject;
            msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();

            await client.ConnectAsync(_smtp.Host, _smtp.Port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_smtp.Username, _smtp.Password);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
        }
    }
}