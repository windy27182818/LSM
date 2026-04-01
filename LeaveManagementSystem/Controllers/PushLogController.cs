using LeaveManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using LeaveManagementSystem.Services;

namespace LeaveManagementSystem.Controllers
{
    [Authorize]
    public class PushLogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly NotificationPushService _push;

        public PushLogController(ApplicationDbContext context, IWebHostEnvironment env, NotificationPushService push)
        {
            _context = context;
            _env = env;
            _push = push;
        }

        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return null;
            return await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
        }

        private string GetLogPath()
        {
            var dir = Path.Combine(_env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "push-log.txt");
        }

        // GET: /PushLog?lines=200
        public async Task<IActionResult> Index(int lines = 200)
        {
            var me = await GetCurrentEmployeeAsync();
            if (me == null) return RedirectToAction("Login", "Account");
            if (!me.IsAdmin) return Unauthorized();

            lines = Math.Clamp(lines, 20, 2000);

            var path = GetLogPath();

            string[] all = Array.Empty<string>();
            if (System.IO.File.Exists(path))
            {
                all = await System.IO.File.ReadAllLinesAsync(path);
            }

            var last = all.Length <= lines ? all : all.Skip(all.Length - lines).ToArray();

            ViewBag.TotalLines = all.Length;
            ViewBag.ShowLines = lines;
            ViewBag.FilePath = path; // 方便你确认路径（只给 admin 看）
            return View(last);
        }

        // POST: /PushLog/Clear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            var me = await GetCurrentEmployeeAsync();
            if (me == null) return RedirectToAction("Login", "Account");
            if (!me.IsAdmin) return Unauthorized();

            var path = GetLogPath();
            await System.IO.File.WriteAllTextAsync(path, "");
            TempData["Msg"] = "push-log.txt 已清空";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Test()
        {
            var me = await GetCurrentEmployeeAsync();
            if (me == null || !me.IsAdmin) return Unauthorized();

            var tokens = await _context.Employees
                .Where(e => !string.IsNullOrWhiteSpace(e.APN))
                .Select(e => e.APN!)
                .ToListAsync();

            await _push.SendToEmployeeAsync("TEST PUSH FROM LMS", tokens);

            return RedirectToAction(nameof(Index));
        }

    }
}
