using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using LeaveManagementSystem.Models;
using LeaveManagementSystem.ViewModels;
using LeaveManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using LeaveManagementSystem.Helpers;

namespace LeaveManagementSystem.Controllers
{
    [Authorize]
    public class LeaveRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly NotificationPushService _push;

        // ✅ NEW: Email + Config
        private readonly IEmailNotificationService _email;
        private readonly IConfiguration _config;

        public LeaveRequestsController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            NotificationPushService push,
            IEmailNotificationService email,          // ✅ NEW
            IConfiguration config)                   // ✅ NEW
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _push = push;

            _email = email;
            _config = config;
        }

        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return null;
            return await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
        }

        // ✅ “真正 Admin” 判定（全公司）
        private async Task<bool> CurrentUserIsAdminAsync()
        {
            var emp = await GetCurrentEmployeeAsync();
            return emp?.IsAdmin == true;
        }

        // ✅ “可审批者” 判定：Admin 或 Boss
        private static bool CanApprove(Employee emp) => emp.IsAdmin || emp.IsBoss;

        private static bool IsDecisionStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;

            return status.Equals("Approved", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Rejected", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatDateRange(DateTime start, DateTime end)
        {
            var s = start.Date.ToString("dd/MM/yyyy");
            var e = end.Date.ToString("dd/MM/yyyy");
            return s == e ? s : $"{s} - {e}";
        }

        private static int CalculateWorkingDaysExcludingPublicHolidays(
            DateTime startDate,
            DateTime endDate,
            HashSet<DateTime> publicHolidayDates
        )
        {
            if (endDate.Date < startDate.Date) return 0;

            int total = 0;

            for (var d = startDate.Date; d <= endDate.Date; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                if (publicHolidayDates.Contains(d))
                    continue;

                total++;
            }

            return total;
        }

        // ===============================
        // ✅ NEW: Email helpers
        // ===============================
        private bool EnableEmail()
            => _config.GetValue<bool>("Notification:EnableEmail", false);

        private bool EnablePush()
            => _config.GetValue<bool>("Notification:EnablePush", true);

        private async Task<List<string>> GetAdminEmailsAsync()
        {
            if (_config.GetValue<bool>("Notification:TestMode", false))
            {
                var test = _config.GetValue<string>("Notification:TestEmail");

                if (!string.IsNullOrWhiteSpace(test))
                {
                    return new List<string> { test.Trim() };
                }

                return new List<string>();
            }

            var configured = _config.GetSection("Notification:AdminEmails").Get<string[]>();
            if (configured != null && configured.Length > 0)
            {
                return configured
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var fromDb = await _context.Employees
                .AsNoTracking()
                .Where(e => e.IsAdmin == true && !string.IsNullOrWhiteSpace(e.Email))
                .Select(e => e.Email!.Trim())
                .Distinct()
                .ToListAsync();

            return fromDb;
        }

        private async Task<List<string>> GetDepartmentRecipientsAsync(string? dept)
        {
            if (string.IsNullOrWhiteSpace(dept))
                return new List<string>();

            var d = dept.Trim().ToLowerInvariant();

            var isSale = d == "sale" || d == "sales";
            var isAcc = d == "acc" || d == "account" || d == "accounts" || d == "accounting";

            if (!isSale && !isAcc)
                return new List<string>();

            var managerEmails = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Department != null
                            && e.Department.ToLower() == d
                            && e.IsManager
                            && !string.IsNullOrWhiteSpace(e.Email))
                .Select(e => e.Email!.Trim())
                .Distinct()
                .ToListAsync();

            return managerEmails;
        }

        private async Task SendEmailSafeAsync(string toEmail, string subject, string htmlBody)
        {
            await SendEmailSafeAsync(new List<string> { toEmail }, subject, htmlBody);
        }

        private async Task SendEmailSafeAsync(List<string> toEmails, string subject, string htmlBody)
        {
            if (!EnableEmail()) return;

            // ✅ TestMode：全部改发到 TestEmail
            if (_config.GetValue<bool>("Notification:TestMode", false))
            {
                var testEmail = _config.GetValue<string>("Notification:TestEmail");
                if (string.IsNullOrWhiteSpace(testEmail))
                    return;

                toEmails = new List<string> { testEmail.Trim() };
            }

            var recipients = (toEmails ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0) return;

            try
            {
                await _email.SendAsync(recipients, subject, htmlBody);
            }
            catch
            {
            }
        }

        private static string HtmlEncodeBasic(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&#39;");
        }

        // ✅ Boss 数据范围过滤（只能 Sale/Acc）
        private static IQueryable<LeaveRequest> ApplyBossScope(IQueryable<LeaveRequest> q)
        {
            return q.Where(l =>
                l.Employee != null &&
                l.Employee.Department != null && (
                    l.Employee.Department.ToLower() == "sale" ||
                    l.Employee.Department.ToLower() == "sales" ||
                    l.Employee.Department.ToLower() == "acc" ||
                    l.Employee.Department.ToLower() == "account" ||
                    l.Employee.Department.ToLower() == "accounts" ||
                    l.Employee.Department.ToLower() == "accounting"
                )
            );
        }

        public async Task<IActionResult> Index()
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            ViewBag.IsAdmin = currentEmp.IsAdmin || currentEmp.IsBoss; // ✅ 让 Boss 看到 Admin UI
            ViewBag.IsBoss = currentEmp.IsBoss;

            IQueryable<LeaveRequest> query = _context.LeaveRequests
                .Include(l => l.Employee)
                .Include(l => l.LeaveType);

            query = query.Where(l => l.Status == "Pending");

            if (currentEmp.IsAdmin)
            {
                // Admin：看全部
            }
            else if (currentEmp.IsBoss)
            {
                // Boss：只看 Sale/Acc
                query = ApplyBossScope(query);
            }
            else
            {
                // 普通员工：只看自己
                query = query.Where(l => l.EmployeeId == currentEmp.Id);
            }

            var leaveRequests = await query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new LeaveRequestListVM
                {
                    Id = l.Id,
                    EmployeeName = l.Employee != null ? (l.Employee.Name ?? "") : "",
                    LeaveTypeName = l.LeaveType != null ? (l.LeaveType.Name ?? "") : "",
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    TotalDays = l.TotalDays,
                    Status = l.Status ?? "",
                    CreatedAt = l.CreatedAt,
                    IsSpecialRequest = l.IsSpecialRequest,
                    AttachmentPath = l.AttachmentPath
                })
                .ToListAsync();

            return View(leaveRequests);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var leaveRequest = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (leaveRequest == null) return NotFound();

            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null)
                return RedirectToAction("Login", "Account");

            ViewBag.IsAdmin = currentEmp.IsAdmin || currentEmp.IsBoss; // ✅ Boss 也当“可审批者”
            ViewBag.IsBoss = currentEmp.IsBoss;

            // ✅ 只限制 Boss：只能看 Sale/Acc
            if (currentEmp.IsBoss)
            {
                if (!DepartmentAccess.IsSaleOrAcc(leaveRequest.Employee?.Department))
                    return Forbid();
            }

            return View(leaveRequest);
        }

        public async Task<IActionResult> Create()
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            ViewData["LeaveTypeId"] = new SelectList(_context.LeaveTypes, "Id", "Name");
            ViewBag.EmployeeName = currentEmp.Name;

            var vm = new LeaveRequestCreateViewModel
            {
                EmployeeId = currentEmp.Id,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today,
                CreatedAt = DateTime.Now
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeaveRequestCreateViewModel model)
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            model.EmployeeId = currentEmp.Id;
            var leaveType = await _context.LeaveTypes.FindAsync(model.LeaveTypeId);

            if (string.IsNullOrWhiteSpace(model.Reason))
            {
                ModelState.AddModelError(nameof(model.Reason), "Reason/Remark is required.");
            }

            double daysRequested = 0;
            var startDate = model.StartDate.Date;
            var endDate = model.EndDate.Date;
            bool isSpecialRequest = false;

            if (endDate < startDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "End Date cannot be earlier than Start Date.");
            }
            else
            {
                var publicHolidayDates = await _context.PublicHolidays
                    .Where(h => h.Date >= startDate && h.Date <= endDate)
                    .Select(h => h.Date)
                    .ToListAsync();

                var holidaySet = publicHolidayDates.Select(d => d.Date).ToHashSet();
                daysRequested = CalculateWorkingDaysExcludingPublicHolidays(startDate, endDate, holidaySet);
            }

            if (leaveType != null)
            {
                bool isHalfDayType =
                    (leaveType.Name ?? "").Contains("Day Off", StringComparison.OrdinalIgnoreCase) ||
                    (leaveType.Name ?? "").Contains("Half", StringComparison.OrdinalIgnoreCase);

                if (isHalfDayType) daysRequested = 0.5;
            }

            if (leaveType != null)
            {
                string typeName = (leaveType.Name ?? string.Empty).ToLowerInvariant();

                bool isMandatoryFile =
                    typeName.Contains("sick") ||
                    typeName.Contains("bereavement") ||
                    typeName.Contains("hospitalisation") ||
                    typeName.Contains("hospitalization") ||
                    typeName.Contains("maternity");

                bool noFile = model.FileAttachment == null || model.FileAttachment.Length == 0;

                if (isMandatoryFile && noFile)
                {
                    string errorMsg =
                        typeName.Contains("sick") ? "You must upload a supporting document (MC) for Sick Leave."
                      : typeName.Contains("bereavement") ? "You must upload a supporting document for Bereavement Leave."
                      : (typeName.Contains("hospitalisation") || typeName.Contains("hospitalization")) ? "You must upload a supporting document for Hospitalisation Leave."
                      : typeName.Contains("maternity") ? "You must upload a supporting document for Maternity Leave."
                      : "You must upload a supporting document for this leave type.";

                    ModelState.AddModelError(nameof(model.FileAttachment), errorMsg);
                }

                if (typeName.Contains("annual") && daysRequested > 3)
                {
                    DateTime minAllowedDate = DateTime.Today.AddDays(30);
                    if (startDate < minAllowedDate)
                    {
                        isSpecialRequest = true;
                    }
                }
            }

            // ===============================
            // Annual Leave Validation (NEW MODEL + CarryForward cap 9)
            // ===============================
            if (leaveType != null)
            {
                var lowerName = (leaveType.Name ?? "").ToLowerInvariant();
                bool isAnnualLeaveType =
                    lowerName.Contains("annual") ||
                    lowerName.Contains("half");

                if (isAnnualLeaveType)
                {
                    double annualRemain = Math.Max(0, currentEmp.AnnualLeaveBalance);
                    double carryRemain = Math.Max(0, currentEmp.AnnualCarryForward);

                    double totalRemain = annualRemain + carryRemain;

                    if (daysRequested > totalRemain)
                    {
                        ModelState.AddModelError(nameof(model.TotalDays),
                            $"You only have {totalRemain} day(s) of Annual Leave remaining.");
                    }
                }
            }

            ModelState.Remove(nameof(model.Status));
            ModelState.Remove(nameof(model.CreatedAt));

            if (!ModelState.IsValid)
            {
                ViewData["LeaveTypeId"] = new SelectList(_context.LeaveTypes, "Id", "Name", model.LeaveTypeId);
                ViewBag.EmployeeName = currentEmp.Name;
                return View(model);
            }

            string? base64String = null;
            string? originalFileName = null;

            // Validate attachment extension early (no heavy reads yet)
            if (model.FileAttachment != null && model.FileAttachment.Length > 0)
            {
                var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg" };
                var ext = Path.GetExtension(model.FileAttachment.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError(nameof(model.FileAttachment), "Only PDF, PNG, JPG files are allowed.");
                    ViewData["LeaveTypeId"] = new SelectList(_context.LeaveTypes, "Id", "Name", model.LeaveTypeId);
                    ViewBag.EmployeeName = currentEmp.Name;
                    return View(model);
                }
            }

            // Fast pre-check to avoid expensive work (especially on slow servers / double-click submit)
            bool preHasOverlap = await _context.LeaveRequests.AnyAsync(l =>
                l.EmployeeId == currentEmp.Id &&
                (l.Status == null ||
                 (l.Status.Trim().ToLower() != "rejected" &&
                  l.Status.Trim().ToLower() != "cancelled")) &&
                startDate <= l.EndDate &&
                endDate >= l.StartDate
            );

            if (preHasOverlap)
            {
                ModelState.AddModelError("", "The date you selected already has a leave application (including those pending approval), so you cannot apply again.\r\n");
                ViewData["LeaveTypeId"] = new SelectList(_context.LeaveTypes, "Id", "Name", model.LeaveTypeId);
                ViewBag.EmployeeName = currentEmp.Name;
                return View(model);
            }

            // Now we can safely read attachment into base64 (only if it passes overlap pre-check)
            if (model.FileAttachment != null && model.FileAttachment.Length > 0)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await model.FileAttachment.CopyToAsync(memoryStream);
                    byte[] fileBytes = memoryStream.ToArray();
                    base64String = Convert.ToBase64String(fileBytes);
                    originalFileName = model.FileAttachment.FileName;
                }
            }

            var leaveRequest = new LeaveRequest
            {
                EmployeeId = currentEmp.Id,
                LeaveTypeId = model.LeaveTypeId,
                StartDate = startDate,
                EndDate = endDate,
                TotalDays = daysRequested,
                Reason = model.Reason?.Trim(),
                Status = "Pending",
                CreatedAt = DateTime.Now,
                IsSpecialRequest = isSpecialRequest,
                AttachmentBase64 = base64String,
                AttachmentPath = originalFileName,

                AnnualCarryForwardUsed = 0,
                AnnualDeductedFromBalance = 0,
                AnnualDeductedFromEntitlement = 0
            };

            // Final concurrency-safe check + insert
            using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            bool hasOverlap = await _context.LeaveRequests.AnyAsync(l =>
                l.EmployeeId == currentEmp.Id &&
                (l.Status == null ||
                 (l.Status.Trim().ToLower() != "rejected" &&
                  l.Status.Trim().ToLower() != "cancelled")) &&
                startDate <= l.EndDate &&
                endDate >= l.StartDate
            );

            if (hasOverlap)
            {
                await tx.RollbackAsync();

                ModelState.AddModelError("", "The date you selected already has a leave application (including those pending approval), so you cannot apply again.\r\n");
                ViewData["LeaveTypeId"] = new SelectList(_context.LeaveTypes, "Id", "Name", model.LeaveTypeId);
                ViewBag.EmployeeName = currentEmp.Name;
                return View(model);
            }

            _context.Add(leaveRequest);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            // ===============================
            // Push Notification (UNCHANGED)
            // ===============================
            try
            {
                if (EnablePush())
                {
                    static string CleanOneLine(string s)
                    {
                        if (string.IsNullOrWhiteSpace(s)) return "";
                        s = s.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
                        while (s.Contains("  ")) s = s.Replace("  ", " ");
                        return s.Trim();
                    }

                    static string Truncate(string s, int max)
                        => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));

                    var employeeName = (currentEmp.Name ?? "").Trim();
                    var leaveTypeName = (leaveType?.Name ?? "").Trim();
                    var dateText = FormatDateRange(startDate, endDate);

                    var rawMsg = $"New Leave Request: {employeeName} - {leaveTypeName} - {dateText}";
                    var msg = Truncate(CleanOneLine(rawMsg), 120);

                    var devices = await _context.Employees
                        .AsNoTracking()
                        .Where(e => !string.IsNullOrWhiteSpace(e.APN))
                        .Select(e => new
                        {
                            Token = e.APN!,
                            DeviceType = e.DeviceType
                        })
                        .ToListAsync();

                    var iosTokens = devices
                        .Where(d => string.IsNullOrWhiteSpace(d.DeviceType) || d.DeviceType.Trim() == "1")
                        .Select(d => d.Token.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct()
                        .ToList();

                    var androidTokens = devices
                        .Where(d => (d.DeviceType ?? "").Trim() == "2")
                        .Select(d => d.Token.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct()
                        .ToList();

                    if (iosTokens.Count > 0 || androidTokens.Count > 0)
                    {
                        var jobcount = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                        var tasks = new List<Task>();
                        if (iosTokens.Count > 0)
                            tasks.Add(_push.SendAsync(msg, iosTokens, jobcount));

                        if (androidTokens.Count > 0)
                            tasks.Add(_push.SendAndroidAsync(msg, androidTokens, jobcount));

                        await Task.WhenAll(tasks);
                    }
                }
            }
            catch
            {
            }

            // ===============================
            // ✅ NEW: Outlook Email to Admins
            // ===============================
            try
            {
                if (EnableEmail())
                {
                    var employeeName = HtmlEncodeBasic((currentEmp.Name ?? "").Trim());
                    var leaveTypeName = HtmlEncodeBasic((leaveType?.Name ?? "").Trim());
                    var dateText = HtmlEncodeBasic(FormatDateRange(startDate, endDate));
                    var reasonText = HtmlEncodeBasic((model.Reason ?? "").Trim());

                    var subject = $"[LMS] New Leave Request - {employeeName}";
                    var link = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/LeaveRequests/Details/{leaveRequest.Id}";

                    var html = $@"
                        <div style='margin:0;padding:0;background:#f5f7fb;'>
                          <div style='max-width:680px;margin:0 auto;padding:24px 12px;font-family:Segoe UI,Arial,sans-serif;color:#111827;'>
    
                            <div style='text-align:left;margin-bottom:14px;'>
                              <div style='font-size:12px;letter-spacing:.08em;color:#6b7280;font-weight:700;'>LEAVE MANAGEMENT SYSTEM</div>
                              <div style='font-size:20px;font-weight:800;margin-top:4px;'>New Leave Request</div>
                              <div style='font-size:13px;color:#6b7280;margin-top:2px;'>A new leave request is awaiting your review.</div>
                            </div>

                            <div style='background:#ffffff;border:1px solid #e5e7eb;border-radius:14px;overflow:hidden;box-shadow:0 6px 18px rgba(17,24,39,0.06);'>
      
                              <div style='padding:14px 18px;background:linear-gradient(90deg,#eef2ff,#ffffff);border-bottom:1px solid #e5e7eb;'>
                                <div style='display:inline-block;padding:6px 10px;border-radius:999px;background:#fff;border:1px solid #e5e7eb;font-size:12px;color:#374151;'>
                                  Status: <b style='color:#f59e0b;'>Pending</b>
                                </div>
                              </div>

                              <div style='padding:18px;'>
                                <table style='width:100%;border-collapse:collapse;font-size:14px;'>
                                  <tr>
                                    <td style='padding:10px 0;color:#6b7280;width:140px;'>Employee</td>
                                    <td style='padding:10px 0;font-weight:700;color:#111827;'>{employeeName}</td>
                                  </tr>
                                  <tr>
                                    <td style='padding:10px 0;color:#6b7280;'>Leave Type</td>
                                    <td style='padding:10px 0;font-weight:600;'>{leaveTypeName}</td>
                                  </tr>
                                  <tr>
                                    <td style='padding:10px 0;color:#6b7280;'>Date</td>
                                    <td style='padding:10px 0;font-weight:600;'>{dateText}</td>
                                  </tr>
                                  <tr>
                                    <td style='padding:10px 0;color:#6b7280;vertical-align:top;'>Reason</td>
                                    <td style='padding:10px 0;'>
                                      <div style='background:#f9fafb;border:1px dashed #e5e7eb;border-radius:10px;padding:12px;line-height:1.5;color:#111827;'>
                                        {(string.IsNullOrWhiteSpace(reasonText) ? "<span style='color:#9ca3af;'>—</span>" : reasonText)}
                                      </div>
                                    </td>
                                  </tr>
                                </table>

                                <div style='margin-top:16px;padding-top:16px;border-top:1px solid #e5e7eb;'>
                                  <a href='{link}'
                                     style='display:inline-block;background:#2563eb;color:#ffffff;text-decoration:none;padding:11px 16px;border-radius:10px;font-weight:700;font-size:14px;'>
                                     Review in LMS →
                                  </a>
                                  <div style='font-size:12px;color:#6b7280;margin-top:10px;'>
                                    If the button doesn’t work, copy and paste this link:<br/>
                                    <span style='color:#374151;word-break:break-all;'>{link}</span>
                                  </div>
                                </div>
                              </div>
                            </div>

                            <div style='font-size:12px;color:#9ca3af;margin-top:14px;line-height:1.4;'>
                              This is an automated message from LMS. Please do not reply to this email.
                            </div>

                          </div>
                        </div>";

                    var deptEmails = await GetDepartmentRecipientsAsync(currentEmp.Department);

                    var recipients = deptEmails;

                    if (recipients == null || recipients.Count == 0)
                    {
                        recipients = await GetAdminEmailsAsync();
                    }

                    await SendEmailSafeAsync(recipients, subject, html);
                }
            }
            catch
            {
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.LeaveRequests == null) return NotFound();

            var leaveRequest = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (leaveRequest == null) return NotFound();

            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            bool canApprove = CanApprove(currentEmp);
            bool isOwner = leaveRequest.EmployeeId == currentEmp.Id;

            // ✅ Boss 审批权限，但只限 Sale/Acc
            if (currentEmp.IsBoss && !DepartmentAccess.IsSaleOrAcc(leaveRequest.Employee?.Department))
                return Forbid();

            if (!canApprove && !isOwner) return NotFound();

            ViewBag.IsAdmin = canApprove;   // ✅ Boss 也显示审批 UI
            ViewBag.IsBoss = currentEmp.IsBoss;

            ViewBag.EmployeeName = leaveRequest.Employee?.Name;
            ViewBag.CurrentEmployeeId = currentEmp.Id;

            ViewData["LeaveTypeId"] = new SelectList(_context.LeaveTypes, "Id", "Name", leaveRequest.LeaveTypeId);

            return View(leaveRequest);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Cancel(int id)
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            var leaveRequest = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (leaveRequest == null) return NotFound();

            if (leaveRequest.EmployeeId != currentEmp.Id)
                return Unauthorized();

            if (!string.Equals(leaveRequest.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                TempData["LeaveError"] = "Cannot cancel a processed request.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            leaveRequest.Status = "Cancelled";
            await _context.SaveChangesAsync();

            try
            {
                if (EnableEmail())
                {
                    var employeeName = HtmlEncodeBasic((leaveRequest.Employee?.Name ?? "").Trim());
                    var leaveTypeName = HtmlEncodeBasic((leaveRequest.LeaveType?.Name ?? "").Trim());
                    var dateText = HtmlEncodeBasic(FormatDateRange(leaveRequest.StartDate, leaveRequest.EndDate));
                    var reasonText = HtmlEncodeBasic((leaveRequest.Reason ?? "").Trim());

                    var subject = $"[LMS] Leave Cancelled - {employeeName}";
                    var link = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/LeaveRequests/Details/{leaveRequest.Id}";

                    var html = $@"
                        <div style='margin:0;padding:0;background:#f5f7fb;'>
                          <div style='max-width:680px;margin:0 auto;padding:24px 12px;font-family:Segoe UI,Arial,sans-serif;color:#111827;'>
                            <div style='text-align:left;margin-bottom:14px;'>
                              <div style='font-size:12px;letter-spacing:.08em;color:#6b7280;font-weight:700;'>LEAVE MANAGEMENT SYSTEM</div>
                              <div style='font-size:20px;font-weight:800;margin-top:4px;'>Leave Request Cancelled</div>
                              <div style='font-size:13px;color:#6b7280;margin-top:2px;'>An employee has cancelled a pending leave request.</div>
                            </div>

                            <div style='background:#ffffff;border:1px solid #e5e7eb;border-radius:14px;overflow:hidden;box-shadow:0 6px 18px rgba(17,24,39,0.06);'>
                              <div style='padding:14px 18px;background:linear-gradient(90deg,#fff7ed,#ffffff);border-bottom:1px solid #e5e7eb;'>
                                <div style='display:inline-block;padding:6px 10px;border-radius:999px;background:#fff;border:1px solid #e5e7eb;font-size:12px;color:#374151;'>
                                  Status: <b style='color:#ef4444;'>Cancelled</b>
                                </div>
                              </div>

                              <div style='padding:18px;'>
                                <table style='width:100%;border-collapse:collapse;font-size:14px;'>
                                  <tr>
                                    <td style='padding:10px 0;color:#6b7280;width:140px;'>Employee</td>
                                    <td style='padding:10px 0;font-weight:700;color:#111827;'>{employeeName}</td>
                                  </tr>
                                  <tr>
                                    <td style='padding:10px 0;color:#6b7280;'>Leave Type</td>
                                    <td style='padding:10px 0;font-weight:600;'>{leaveTypeName}</td>
                                  </tr>
                                  <tr>
                                    <td style='padding:10px 0;color:#6b7280;'>Date</td>
                                    <td style='padding:10px 0;font-weight:600;'>{dateText}</td>
                                  </tr>
                                  {(string.IsNullOrWhiteSpace(reasonText) ? "" : $@"
                                  <tr>
                                    <td style='padding:10px 0;color:#6b7280;vertical-align:top;'>Reason</td>
                                    <td style='padding:10px 0;'>
                                      <div style='background:#f9fafb;border:1px dashed #e5e7eb;border-radius:10px;padding:12px;line-height:1.5;color:#111827;'>
                                        {reasonText}
                                      </div>
                                    </td>
                                  </tr>")}
                                </table>

                                <div style='margin-top:16px;padding-top:16px;border-top:1px solid #e5e7eb;'>
                                  <a href='{link}'
                                     style='display:inline-block;background:#111827;color:#ffffff;text-decoration:none;padding:11px 16px;border-radius:10px;font-weight:700;font-size:14px;'>
                                     Open in LMS →
                                  </a>
                                  <div style='font-size:12px;color:#6b7280;margin-top:10px;'>
                                    If the button doesn’t work, copy and paste this link:<br/>
                                    <span style='color:#374151;word-break:break-all;'>{link}</span>
                                  </div>
                                </div>
                              </div>
                            </div>

                            <div style='font-size:12px;color:#9ca3af;margin-top:14px;line-height:1.4;'>
                              This is an automated message from LMS. Please do not reply to this email.
                            </div>
                          </div>
                        </div>";

                    var deptEmails = await GetDepartmentRecipientsAsync(leaveRequest.Employee?.Department);

                    var recipients = deptEmails;
                    if (recipients == null || recipients.Count == 0)
                    {
                        recipients = await GetAdminEmailsAsync();
                    }

                    await SendEmailSafeAsync(recipients, subject, html);
                }
            }
            catch
            {
            }

            TempData["LeaveSuccess"] = "Leave request cancelled successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,EmployeeId,LeaveTypeId,StartDate,EndDate,TotalDays,Reason,Status,CreatedAt,ApproverRemark,AttachmentPath")] LeaveRequest formModel,
            IFormFile? NewFileAttachment)
        {
            if (id != formModel.Id) return NotFound();

            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            bool canApprove = CanApprove(currentEmp);

            var entity = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (entity == null) return NotFound();

            // ✅ Boss 审批权限，但只限 Sale/Acc（防 URL 越权）
            if (currentEmp.IsBoss && !DepartmentAccess.IsSaleOrAcc(entity.Employee?.Department))
                return Forbid();

            bool isOwner = entity.EmployeeId == currentEmp.Id;
            if (!canApprove && !isOwner) return Unauthorized();

            // 员工改申请时仍需要 Reason
            if (!canApprove && string.IsNullOrWhiteSpace(formModel.Reason))
            {
                ModelState.AddModelError(nameof(formModel.Reason), "Reason/Remark is required.");
            }

            // 审批（Admin/Boss）决定状态必须写 Remark
            if (canApprove && IsDecisionStatus(formModel.Status) && string.IsNullOrWhiteSpace(formModel.ApproverRemark))
            {
                ModelState.AddModelError(nameof(formModel.ApproverRemark), "Approver Remark is required when approving, rejecting, or cancelling.");
            }

            if (NewFileAttachment != null && NewFileAttachment.Length > 0)
            {
                var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg" };
                var ext = Path.GetExtension(NewFileAttachment.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError("NewFileAttachment", "Only PDF, PNG, JPG files are allowed.");
                }
                else
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await NewFileAttachment.CopyToAsync(memoryStream);
                        byte[] fileBytes = memoryStream.ToArray();
                        entity.AttachmentBase64 = Convert.ToBase64String(fileBytes);
                        entity.AttachmentPath = NewFileAttachment.FileName;
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.IsAdmin = canApprove;
                ViewBag.IsBoss = currentEmp.IsBoss;
                ViewBag.EmployeeName = entity.Employee?.Name;
                ViewData["LeaveTypeId"] = new SelectList(_context.LeaveTypes, "Id", "Name", entity.LeaveTypeId);
                return View(entity);
            }

            var previousStatusForPush = (entity.Status ?? "").Trim();
            var newStatusForPush = (formModel.Status ?? "").Trim();

            if (canApprove)
            {
                var previousStatus = (entity.Status ?? "").Trim();
                var newStatus = (formModel.Status ?? "").Trim();

                entity.Status = newStatus;
                entity.ApproverRemark = formModel.ApproverRemark?.Trim();
                entity.ApproverId = currentEmp.Id;

                bool prevDecision = IsDecisionStatus(previousStatus);
                bool newDecision = IsDecisionStatus(newStatus);

                if (newDecision)
                    entity.ApprovedDate = DateTime.Now;
                else if (prevDecision && !newDecision)
                    entity.ApprovedDate = null;

                var employee = entity.Employee;
                if (employee != null)
                {
                    string ltName = entity.LeaveType?.Name ?? string.Empty;

                    bool isAnnualLeave =
                        ltName.Contains("annual", StringComparison.OrdinalIgnoreCase) ||
                        ltName.Contains("half", StringComparison.OrdinalIgnoreCase);

                    bool isSickLeave = ltName.Contains("sick", StringComparison.OrdinalIgnoreCase);

                    double totalDays = entity.TotalDays;
                    int totalDaysInt = (int)Math.Ceiling(entity.TotalDays);

                    // =========================
                    // Annual Leave (Carry Forward + Current Year)
                    // =========================
                    if (isAnnualLeave)
                    {
                        bool wasApproved = string.Equals(previousStatus, "Approved", StringComparison.OrdinalIgnoreCase);
                        bool nowApproved = string.Equals(newStatus, "Approved", StringComparison.OrdinalIgnoreCase);

                        static double Clamp(double v) => v < 0 ? 0 : v;

                        double carry = Clamp(employee.AnnualCarryForward);
                        double thisYear = Clamp(employee.AnnualLeaveBalance);
                        double totalRemain = carry + thisYear;

                        if (!wasApproved && nowApproved)
                        {
                            if (totalDays > totalRemain)
                            {
                                ModelState.AddModelError("Status",
                                    $"Cannot approve. {employee.Name} only has {totalRemain} day(s) of Annual Leave remaining.");

                                ViewBag.IsAdmin = canApprove;
                                ViewBag.IsBoss = currentEmp.IsBoss;
                                ViewBag.EmployeeName = entity.Employee?.Name;
                                ViewData["LeaveTypeId"] = new SelectList(_context.LeaveTypes, "Id", "Name", entity.LeaveTypeId);
                                return View(entity);
                            }

                            double need = totalDays;

                            double useThisYear = Math.Min(thisYear, need);
                            thisYear -= useThisYear;
                            need -= useThisYear;

                            double useCarry = 0;
                            if (need > 0)
                            {
                                useCarry = Math.Min(carry, need);
                                carry -= useCarry;
                                need -= useCarry;
                            }

                            employee.AnnualCarryForward = Clamp(carry);
                            employee.AnnualLeaveBalance = Clamp(thisYear);

                            employee.AnnualLeaveUsed = Math.Max(0, employee.AnnualLeaveUsed + totalDays);
                        }

                        if (wasApproved && !nowApproved &&
                            (string.Equals(newStatus, "Rejected", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(newStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)))
                        {
                            double back = totalDays;

                            thisYear += back;
                            double maxThisYear = employee.AnnualLeaveEntitlement;
                            if (thisYear > maxThisYear)
                            {
                                double extra = thisYear - maxThisYear;
                                thisYear = maxThisYear;
                                carry += extra;
                            }

                            employee.AnnualCarryForward = Clamp(carry);
                            employee.AnnualLeaveBalance = Clamp(thisYear);

                            employee.AnnualLeaveUsed = Math.Max(0, employee.AnnualLeaveUsed - totalDays);
                        }

                        if (employee.AnnualCarryForward < 0) employee.AnnualCarryForward = 0;
                        if (employee.AnnualLeaveBalance < 0) employee.AnnualLeaveBalance = 0;
                    }

                    // =========================
                    // Sick Leave (unchanged)
                    // =========================
                    if (isSickLeave)
                    {
                        bool wasApproved = string.Equals(previousStatus, "Approved", StringComparison.OrdinalIgnoreCase);
                        bool nowApproved = string.Equals(newStatus, "Approved", StringComparison.OrdinalIgnoreCase);

                        if (!wasApproved && nowApproved)
                        {
                            if (totalDaysInt > employee.SickLeaveBalance)
                            {
                                ModelState.AddModelError("Status",
                                    $"Cannot approve. {employee.Name} only has {employee.SickLeaveBalance} day(s) of Sick Leave remaining.");

                                ViewBag.IsAdmin = canApprove;
                                ViewBag.IsBoss = currentEmp.IsBoss;
                                ViewBag.EmployeeName = entity.Employee?.Name;
                                ViewData["LeaveTypeId"] = new SelectList(_context.LeaveTypes, "Id", "Name", entity.LeaveTypeId);
                                return View(entity);
                            }

                            employee.SickLeaveUsed += totalDaysInt;
                            employee.SickLeaveBalance = employee.SickLeaveEntitlement - employee.SickLeaveUsed;
                        }

                        if (wasApproved && !nowApproved &&
                            (string.Equals(newStatus, "Rejected", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(newStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)))
                        {
                            employee.SickLeaveUsed -= totalDaysInt;
                            if (employee.SickLeaveUsed < 0) employee.SickLeaveUsed = 0;

                            employee.SickLeaveBalance = employee.SickLeaveEntitlement - employee.SickLeaveUsed;
                        }

                        if (employee.SickLeaveBalance < 0) employee.SickLeaveBalance = 0;
                    }
                }
            }
            else
            {
                entity.LeaveTypeId = formModel.LeaveTypeId;
                entity.StartDate = formModel.StartDate.Date;
                entity.EndDate = formModel.EndDate.Date;

                var s = entity.StartDate.Date;
                var e = entity.EndDate.Date;

                if (e < s)
                {
                    ModelState.AddModelError(nameof(formModel.EndDate), "End Date cannot be earlier than Start Date.");
                    ViewBag.IsAdmin = canApprove;
                    ViewBag.IsBoss = currentEmp.IsBoss;
                    ViewBag.EmployeeName = entity.Employee?.Name;
                    ViewData["LeaveTypeId"] = new SelectList(_context.LeaveTypes, "Id", "Name", entity.LeaveTypeId);
                    return View(entity);
                }

                var publicHolidayDates = await _context.PublicHolidays
                    .Where(h => h.Date >= s && h.Date <= e)
                    .Select(h => h.Date)
                    .ToListAsync();

                var holidaySet = publicHolidayDates.Select(d => d.Date).ToHashSet();

                double recalculatedDays = CalculateWorkingDaysExcludingPublicHolidays(s, e, holidaySet);

                var lt = await _context.LeaveTypes.FindAsync(entity.LeaveTypeId);
                if (lt != null)
                {
                    bool isHalfDayType =
                        (lt.Name ?? "").Contains("Day Off", StringComparison.OrdinalIgnoreCase) ||
                        (lt.Name ?? "").Contains("Half", StringComparison.OrdinalIgnoreCase);

                    if (isHalfDayType) recalculatedDays = 0.5;
                }

                entity.TotalDays = recalculatedDays;
                entity.Reason = formModel.Reason?.Trim();

                entity.Status = "Pending";
                entity.ApprovedDate = null;
                entity.ApproverRemark = null;
            }

            try
            {
                await _context.SaveChangesAsync();

                if (canApprove)
                {
                    var prev = (previousStatusForPush ?? "").Trim();
                    var now = (newStatusForPush ?? "").Trim();

                    if (!string.Equals(prev, now, StringComparison.OrdinalIgnoreCase))
                    {
                        bool shouldPush =
                            now.Equals("Approved", StringComparison.OrdinalIgnoreCase) ||
                            now.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ||
                            now.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);

                        if (shouldPush)
                        {
                            // ===============================
                            // Push to employee (UNCHANGED)
                            // ===============================
                            try
                            {
                                if (EnablePush())
                                {
                                    static string CleanOneLine(string s)
                                    {
                                        if (string.IsNullOrWhiteSpace(s)) return "";
                                        s = s.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
                                        while (s.Contains("  ")) s = s.Replace("  ", " ");
                                        return s.Trim();
                                    }

                                    static string Truncate(string s, int max)
                                        => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));

                                    var leaveTypeName = (entity.LeaveType?.Name ?? "").Trim();
                                    var dateText = FormatDateRange(entity.StartDate, entity.EndDate);

                                    var rawMsg = $"Leave {now}: {leaveTypeName} ({dateText})";
                                    var msg = Truncate(CleanOneLine(rawMsg), 120);

                                    var empToken = (entity.Employee?.APN ?? "").Trim();
                                    var deviceType = (entity.Employee?.DeviceType ?? "").Trim();

                                    if (!string.IsNullOrWhiteSpace(empToken))
                                    {
                                        var pushJobcount = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                                        if (deviceType == "2")
                                            await _push.SendAndroidAsync(msg, new[] { empToken }, pushJobcount);
                                        else
                                            await _push.SendAsync(msg, new[] { empToken }, pushJobcount);
                                    }
                                }
                            }
                            catch
                            {
                            }

                            try
                            {
                                if (EnableEmail())
                                {
                                    var empEmail = (entity.Employee?.Email ?? "").Trim();
                                    if (!string.IsNullOrWhiteSpace(empEmail))
                                    {
                                        var leaveTypeName = HtmlEncodeBasic((entity.LeaveType?.Name ?? "").Trim());
                                        var dateText = HtmlEncodeBasic(FormatDateRange(entity.StartDate, entity.EndDate));
                                        var approverRemark = HtmlEncodeBasic((entity.ApproverRemark ?? "").Trim());

                                        var subject = $"[LMS] Leave {now} - {leaveTypeName}";
                                        var link = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/LeaveRequests/Details/{entity.Id}";

                                        var statusText = HtmlEncodeBasic(now);

                                        var statusColor =
                                            now.Equals("Approved", StringComparison.OrdinalIgnoreCase) ? "#16a34a" :
                                            now.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ? "#dc2626" :
                                            now.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ? "#6b7280" :
                                            "#2563eb";

                                        var html = $@"
                                            <div style='margin:0;padding:0;background:#f5f7fb;'>
                                              <div style='max-width:680px;margin:0 auto;padding:24px 12px;font-family:Segoe UI,Arial,sans-serif;color:#111827;'>
                                                <div style='margin-bottom:14px;'>
                                                  <div style='font-size:12px;letter-spacing:.08em;color:#6b7280;font-weight:700;'>LEAVE MANAGEMENT SYSTEM</div>
                                                  <div style='font-size:20px;font-weight:800;margin-top:4px;'>Leave Request Update</div>
                                                  <div style='font-size:13px;color:#6b7280;margin-top:2px;'>Your leave request has been processed.</div>
                                                </div>

                                                <div style='background:#ffffff;border:1px solid #e5e7eb;border-radius:14px;overflow:hidden;box-shadow:0 6px 18px rgba(17,24,39,0.06);'>
                                                  <div style='padding:14px 18px;background:linear-gradient(90deg,#eef2ff,#ffffff);border-bottom:1px solid #e5e7eb;'>
                                                    <div style='display:inline-block;padding:6px 10px;border-radius:999px;background:#fff;border:1px solid #e5e7eb;font-size:12px;color:#374151;'>
                                                      Status: <b style='color:{statusColor};'>{statusText}</b>
                                                    </div>
                                                  </div>

                                                  <div style='padding:18px;'>
                                                    <table style='width:100%;border-collapse:collapse;font-size:14px;'>
                                                      <tr>
                                                        <td style='padding:10px 0;color:#6b7280;width:140px;'>Leave Type</td>
                                                        <td style='padding:10px 0;font-weight:600;'>{leaveTypeName}</td>
                                                      </tr>
                                                      <tr>
                                                        <td style='padding:10px 0;color:#6b7280;'>Date</td>
                                                        <td style='padding:10px 0;font-weight:600;'>{dateText}</td>
                                                      </tr>

                                                      {(string.IsNullOrWhiteSpace(approverRemark) ? "" : $@"
                                                      <tr>
                                                        <td style='padding:10px 0;color:#6b7280;vertical-align:top;'>Remark</td>
                                                        <td style='padding:10px 0;'>
                                                          <div style='background:#f9fafb;border:1px dashed #e5e7eb;border-radius:10px;padding:12px;line-height:1.5;color:#111827;'>
                                                            {approverRemark}
                                                          </div>
                                                        </td>
                                                      </tr>")}
                                                    </table>

                                                    <div style='margin-top:16px;padding-top:16px;border-top:1px solid #e5e7eb;'>
                                                      <a href='{link}'
                                                         style='display:inline-block;background:#2563eb;color:#ffffff;text-decoration:none;padding:11px 16px;border-radius:10px;font-weight:700;font-size:14px;'>
                                                         View Details in LMS →
                                                      </a>

                                                      <div style='font-size:12px;color:#6b7280;margin-top:10px;'>
                                                        If the button doesn’t work, copy and paste this link:<br/>
                                                        <span style='color:#374151;word-break:break-all;'>{link}</span>
                                                      </div>
                                                    </div>
                                                  </div>
                                                </div>

                                                <div style='font-size:12px;color:#9ca3af;margin-top:14px;line-height:1.4;'>
                                                  This is an automated message from LMS. Please do not reply to this email.
                                                </div>
                                              </div>
                                            </div>";

                                        await SendEmailSafeAsync(empEmail, subject, html);
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.LeaveRequests.Any(e => e.Id == entity.Id)) return NotFound();
                else throw;
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var leaveRequest = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (leaveRequest == null) return NotFound();

            // ✅ Delete 仍保留给真正 Admin（不建议 Boss 删除记录）
            if (!await CurrentUserIsAdminAsync()) return Unauthorized();

            return View(leaveRequest);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await CurrentUserIsAdminAsync()) return Unauthorized();

            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest != null)
            {
                _context.LeaveRequests.Remove(leaveRequest);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ApprovedList()
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            if (!CanApprove(currentEmp)) return Unauthorized();

            IQueryable<LeaveRequest> q = _context.LeaveRequests
                .Include(x => x.Employee)
                .Include(x => x.LeaveType)
                .Where(x => x.Status == "Approved");

            if (currentEmp.IsBoss && !currentEmp.IsAdmin)
                q = ApplyBossScope(q);

            var list = await q
                .OrderByDescending(x => x.ApprovedDate ?? x.CreatedAt)
                .ToListAsync();

            ViewBag.IsAdmin = CanApprove(currentEmp);
            ViewBag.IsBoss = currentEmp.IsBoss;

            return View(list);
        }

        public async Task<IActionResult> DownloadAttachment(int? id)
        {
            if (id == null) return NotFound();

            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            // 需要拿到 Department 来做 Boss 限制
            var leaveRequest = await _context.LeaveRequests
                .Include(m => m.Employee)
                .Where(m => m.Id == id)
                .Select(m => new
                {
                    m.AttachmentBase64,
                    m.AttachmentPath,
                    Dept = m.Employee != null ? m.Employee.Department : null
                })
                .FirstOrDefaultAsync();

            if (leaveRequest == null || string.IsNullOrEmpty(leaveRequest.AttachmentBase64))
            {
                return NotFound("No attachment found for this request.");
            }

            // ✅ Boss 只能下载 Sale/Acc 的附件
            if (currentEmp.IsBoss)
            {
                if (!DepartmentAccess.IsSaleOrAcc(leaveRequest.Dept))
                    return Forbid();
            }

            byte[] fileBytes;
            try
            {
                fileBytes = Convert.FromBase64String(leaveRequest.AttachmentBase64);
            }
            catch (FormatException)
            {
                return BadRequest("Invalid file format in database.");
            }

            string fileName = leaveRequest.AttachmentPath ?? "document.pdf";
            string contentType = "application/octet-stream";
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (ext == ".pdf") contentType = "application/pdf";
            else if (ext == ".png") contentType = "image/png";
            else if (ext == ".jpg" || ext == ".jpeg") contentType = "image/jpeg";

            return File(fileBytes, contentType, fileName);
        }

        [HttpGet]
        public async Task<IActionResult> CalculateTotalDays(DateTime startDate, DateTime endDate, int leaveTypeId)
        {
            var start = startDate.Date;
            var end = endDate.Date;

            if (end < start)
                return Json(new { totalDays = 0 });

            var publicHolidayDates = await _context.PublicHolidays
                .Where(h => h.Date >= start && h.Date <= end)
                .Select(h => h.Date)
                .ToListAsync();

            var holidaySet = publicHolidayDates.Select(d => d.Date).ToHashSet();

            double days = CalculateWorkingDaysExcludingPublicHolidays(start, end, holidaySet);

            var leaveType = await _context.LeaveTypes.FindAsync(leaveTypeId);
            if (leaveType != null)
            {
                bool isHalfDayType =
                    (leaveType.Name ?? "").Contains("Day Off", StringComparison.OrdinalIgnoreCase) ||
                    (leaveType.Name ?? "").Contains("Half", StringComparison.OrdinalIgnoreCase);

                if (isHalfDayType) days = 0.5;
            }

            return Json(new { totalDays = days });
        }
    }
}
