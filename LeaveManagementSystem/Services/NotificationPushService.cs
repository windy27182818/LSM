using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text;
using System.Text.Json;

namespace LeaveManagementSystem.Services
{
    public class NotificationPushService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly IHostEnvironment _env;

        public NotificationPushService(HttpClient http, IConfiguration config, IHostEnvironment env)
        {
            _http = http;
            _config = config;
            _env = env;
        }

        private string GetLogPath()
        {
            var dir = Path.Combine(_env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "push-log.txt");
        }

        private void Log(string line)
        {
            try
            {
                var path = GetLogPath();
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {line}{Environment.NewLine}");
            }
            catch { }
        }

        private string GetIosUrl()
            => _config["PushSettings:IosPushUrl"]
               ?? "https://wp.jds.com.my/API/jsonPost/iOS/CCS/jsonp8push.php";

        private string GetAndroidUrl()
            => _config["PushSettings:AndroidPushUrl"]
               ?? "https://wp.jds.com.my/API/jsonPost/Android/CCS/pushAndBase.php";

        private async Task SendInternalAsync(string platform, string url, string message, IEnumerable<string>? tokens, string jobcount)
        {
            var tokenList = (tokens?.ToArray() ?? Array.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct()
                .ToArray();

            if (tokenList.Length == 0)
            {
                Log($"SKIP | platform={platform} | tokens=0 | msg={message}");
                return;
            }

            var payload = new
            {
                devicetoken = tokenList,
                message = message,
                jobcount = jobcount
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                Log($"SEND | platform={platform} | tokens={tokenList.Length} | url={url} | msg={message}");

                var resp = await _http.PostAsync(url, content);
                var respBody = await resp.Content.ReadAsStringAsync();

                Log($"RESP | platform={platform} | status={(int)resp.StatusCode} {resp.StatusCode} | body={(string.IsNullOrWhiteSpace(respBody) ? "<empty>" : respBody)}");

                resp.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Log($"ERR  | platform={platform} | {ex.GetType().Name} | {ex.Message}");
                throw;
            }
        }

        // ✅ 兼容旧代码：默认还是 iOS
        public Task SendAsync(string message, IEnumerable<string>? tokens = null, string jobcount = "1")
        {
            return SendInternalAsync("iOS", GetIosUrl(), message, tokens, jobcount);
        }

        // ✅ 新增：发 Android
        public Task SendAndroidAsync(string message, IEnumerable<string>? tokens = null, string jobcount = "1")
        {
            return SendInternalAsync("Android", GetAndroidUrl(), message, tokens, jobcount);
        }

        // ✅ 旧的：发给 Admin（iOS）
        public Task SendToAdminsAsync(string message, string jobcount = "1")
        {
            var adminTokens = _config.GetSection("PushSettings:AdminTokens").Get<string[]>()
                              ?? Array.Empty<string>();

            return SendAsync(message, adminTokens, jobcount);
        }

        // ✅ 新增：发给 Admin（Android）
        public Task SendToAdminsAndroidAsync(string message, string jobcount = "1")
        {
            var adminTokens = _config.GetSection("PushSettings:AdminAndroidTokens").Get<string[]>()
                              ?? Array.Empty<string>();

            return SendAndroidAsync(message, adminTokens, jobcount);
        }

        // ✅ 新增：Admin 两边都发（用途一样就一次 call 搞定）
        public async Task SendToAdminsBothAsync(string message, string jobcount = "1")
        {
            var ios = _config.GetSection("PushSettings:AdminTokens").Get<string[]>()
                      ?? Array.Empty<string>();

            var andr = _config.GetSection("PushSettings:AdminAndroidTokens").Get<string[]>()
                       ?? Array.Empty<string>();

            var tasks = new List<Task>();

            if (ios.Any(t => !string.IsNullOrWhiteSpace(t)))
                tasks.Add(SendAsync(message, ios, jobcount));

            if (andr.Any(t => !string.IsNullOrWhiteSpace(t)))
                tasks.Add(SendAndroidAsync(message, andr, jobcount));

            if (tasks.Count == 0)
            {
                Log($"SKIP | both | tokens=0 | msg={message}");
                return;
            }

            await Task.WhenAll(tasks);
        }

        // 你原本的员工方法：保持不变（默认 iOS）
        public Task SendToEmployeeAsync(string message, IEnumerable<string> employeeTokens, string jobcount = "1")
        {
            return SendAsync(message, employeeTokens, jobcount);
        }

        // ✅ 新增：员工 Android
        public Task SendToEmployeeAndroidAsync(string message, IEnumerable<string> employeeTokens, string jobcount = "1")
        {
            return SendAndroidAsync(message, employeeTokens, jobcount);
        }
    }
}
