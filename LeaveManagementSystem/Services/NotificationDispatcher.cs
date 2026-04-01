using Microsoft.Extensions.Configuration;

namespace LeaveManagementSystem.Services
{
    public interface INotificationDispatcher
    {
        Task NotifyAdminsAsync(string title, string message, string? url = null, string jobcount = "1");
    }

    public class NotificationDispatcher : INotificationDispatcher
    {
        private readonly IConfiguration _config;
        private readonly NotificationPushService _push;
        private readonly IEmailNotificationService _email;

        public NotificationDispatcher(IConfiguration config, NotificationPushService push, IEmailNotificationService email)
        {
            _config = config;
            _push = push;
            _email = email;
        }

        public async Task NotifyAdminsAsync(string title, string message, string? url = null, string jobcount = "1")
        {
            var enablePush = _config.GetValue<bool>("Notification:EnablePush");
            var enableEmail = _config.GetValue<bool>("Notification:EnableEmail");

            var tasks = new List<Task>();

            // 1) Push
            if (enablePush)
            {
                tasks.Add(_push.SendToAdminsBothAsync($"{title}: {message}", jobcount));
            }

            // 2) Email
            if (enableEmail)
            {
                var adminEmails = _config.GetSection("Notification:AdminEmails").Get<string[]>() ?? Array.Empty<string>();

                var html = $@"
<div style='font-family:Segoe UI,Arial; font-size:14px;'>
  <h3>{title}</h3>
  <p>{message}</p>
  {(string.IsNullOrWhiteSpace(url) ? "" : $"<p><a href='{url}'>Open in LMS</a></p>")}
  <hr/>
  <p style='color:#666;'>Automated message from LMS.</p>
</div>";

                foreach (var to in adminEmails.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    tasks.Add(_email.SendAsync(to.Trim(), title, html));
                }
            }

            if (tasks.Count == 0) return;
            await Task.WhenAll(tasks);
        }
    }
}
