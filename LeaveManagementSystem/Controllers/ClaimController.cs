using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LeaveManagementSystem.Models;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Http;
using LeaveManagementSystem.Helpers; // ✅ 用你的 DepartmentAccess helper（方案A）

namespace LeaveManagementSystem.Controllers
{
    [Authorize]
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string BusinessTripTypeName = "Business Trip";

        public ClaimsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // ✅ Current Employee Helper
        // =========================
        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            var userIdStr = User.FindFirst("UserId")?.Value ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int empId) || empId <= 0) return null;

            return await _context.Employees.FirstOrDefaultAsync(e => e.Id == empId);
        }

        // ✅ 这个方法只用于「非 EF Query」的 if 判断（Forbid 那些），OK
        private static bool IsSaleOrAccDept(string? dept)
            => DepartmentAccess.IsSaleOrAcc(dept);

        // ✅ 这个用于 EF Query (Where) 过滤：必须是 SQL 可翻译表达式
        private static bool IsSaleOrAccDeptSql(string? dept)
        {
            // ⚠️ 注意：这个方法不要在 EF Query 里直接调用
            // 这里保留只是给你看逻辑，真正 Where 里我们会 inline 条件
            return IsSaleOrAccDept(dept);
        }

        private static (string contentType, string ext) GuessContentTypeAndExt(byte[] bytes)
        {
            if (bytes.Length >= 4 &&
                bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
                return ("application/pdf", ".pdf"); // %PDF

            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return ("image/png", ".png");

            if (bytes.Length >= 3 &&
                bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return ("image/jpeg", ".jpg");

            return ("application/octet-stream", ".bin");
        }

        // =========================
        // ✅ Visibility Scope
        // =========================
        private IQueryable<ClaimRequest> ApplyVisibilityScope(IQueryable<ClaimRequest> query, Employee currentEmp)
        {
            // Admin：不限制
            if (currentEmp.IsAdmin) return query;

            // Boss：只看 Sale + Acc 的申请
            // ✅ 这里必须用 EF 可翻译的条件，不能调用自定义方法
            if (currentEmp.IsBoss)
            {
                return query.Where(c =>
                    c.Employee != null &&
                    c.Employee.Department != null &&
                    (
                        c.Employee.Department.Trim().ToLower() == "sale" ||
                        c.Employee.Department.Trim().ToLower() == "sales" ||
                        c.Employee.Department.Trim().ToLower() == "acc" ||
                        c.Employee.Department.Trim().ToLower() == "account" ||
                        c.Employee.Department.Trim().ToLower() == "accounts" ||
                        c.Employee.Department.Trim().ToLower() == "accounting"
                    )
                );
            }

            // 普通员工：调用方自己加 EmployeeId 过滤
            return query;
        }

        // =========================
        // Index
        // =========================
        public async Task<IActionResult> Index()
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            // ✅ 给 Layout / View 用
            ViewBag.IsAdmin = currentEmp.IsAdmin;
            ViewBag.IsBoss = currentEmp.IsBoss;

            var query = _context.ClaimRequests
                .Include(c => c.Employee)
                .Include(c => c.ClaimType)
                .AsQueryable();

            // ✅ 你原本逻辑：只看 Pending
            query = query.Where(c => c.Status == "Pending");

            // ✅ 套可见范围
            if (!currentEmp.IsAdmin && !currentEmp.IsBoss)
            {
                query = query.Where(c => c.EmployeeId == currentEmp.Id);
            }
            else
            {
                query = ApplyVisibilityScope(query, currentEmp);
            }

            var claims = await query
                .OrderByDescending(c => c.RequestDate)
                .ToListAsync();

            foreach (var item in claims)
            {
                if (item.Employee != null)
                {
                    await UpdateEmployeeEligibilityAsync(item.Employee);
                }
            }

            return View(claims);
        }

        // =========================
        // Create/Edit/Form (GET)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Create() => await Form(0);

        [HttpGet]
        public async Task<IActionResult> Edit(int id) => await Form(id);

        [HttpGet]
        public async Task<IActionResult> Form(int? id)
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            // ✅ 给 Layout / View 用
            ViewBag.IsAdmin = currentEmp.IsAdmin;
            ViewBag.IsBoss = currentEmp.IsBoss;

            ClaimRequestFormViewModel model;

            if (id == null || id == 0)
            {
                await UpdateEmployeeEligibilityAsync(currentEmp);

                model = new ClaimRequestFormViewModel
                {
                    Id = 0,
                    EmployeeId = currentEmp.Id,
                    EmployeeName = currentEmp.Name ?? "Unknown",
                    Status = "Pending",
                    CreatedAt = DateTime.Now
                };
            }
            else
            {
                var request = await _context.ClaimRequests
                    .Include(c => c.Employee)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (request == null) return NotFound();

                // ✅ 可见性控制：
                // Admin：可看全部
                // Boss：只能看 Sale/Acc 的
                // 员工：只能看自己的
                if (!currentEmp.IsAdmin)
                {
                    if (currentEmp.IsBoss)
                    {
                        if (request.Employee == null || !IsSaleOrAccDept(request.Employee.Department))
                            return Forbid();
                    }
                    else
                    {
                        if (request.EmployeeId != currentEmp.Id) return Forbid();
                    }
                }

                model = new ClaimRequestFormViewModel
                {
                    Id = request.Id,
                    EmployeeId = request.EmployeeId,
                    EmployeeName = request.Employee?.Name,
                    Amount = request.Amount,
                    Description = request.Description,
                    ClaimTypeId = request.ClaimTypeId,
                    Receipt = request.Receipt,
                    Status = request.Status,
                    ApprovedDate = request.ApprovedDate,
                    ApproverRemarks = request.ApproverRemarks,
                    CreatedAt = request.RequestDate
                };
            }

            // ✅ 你原本 view 用 model.IsAdmin 判断是否有审批权限
            model.IsAdmin = currentEmp.IsAdmin || currentEmp.IsBoss;

            model.CurrentUserId = currentEmp.Id;
            ViewBag.ClaimTypeList = new SelectList(_context.ClaimTypes, "Id", "Name", model.ClaimTypeId);

            return View(model.Id == 0 ? "Create" : "Edit", model);
        }

        // =========================
        // Cancel (POST) - 保持：只能本人取消
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            ViewBag.IsAdmin = currentEmp.IsAdmin;
            ViewBag.IsBoss = currentEmp.IsBoss;

            var claim = await _context.ClaimRequests.FirstOrDefaultAsync(c => c.Id == id);
            if (claim == null) return NotFound();

            // ✅ 只允许本人取消
            if (claim.EmployeeId != currentEmp.Id)
                return Forbid();

            // ✅ 只允许 Pending
            if (!string.Equals(claim.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only pending claims can be cancelled.";
                return RedirectToAction(nameof(Form), new { id });
            }

            claim.Status = "Cancelled";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Claim request cancelled successfully.";
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // Form (POST) - 保存/审批
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Form(ClaimRequestFormViewModel model, IFormFile? file)
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            ViewBag.IsAdmin = currentEmp.IsAdmin;
            ViewBag.IsBoss = currentEmp.IsBoss;

            ModelState.Remove(nameof(model.EmployeeName));
            ModelState.Remove(nameof(model.Receipt));

            if (model.Id == 0 && (file == null || file.Length == 0))
            {
                ModelState.AddModelError("Receipt", "Receipt is required for new claims.");
            }

            if (!ModelState.IsValid)
            {
                model.IsAdmin = currentEmp.IsAdmin || currentEmp.IsBoss;
                model.CurrentUserId = currentEmp.Id;
                ViewBag.ClaimTypeList = new SelectList(_context.ClaimTypes, "Id", "Name", model.ClaimTypeId);
                return View(model.Id == 0 ? "Create" : "Edit", model);
            }

            // ✅ Business Trip 规则保留
            var businessTripId = await GetBusinessTripTypeIdAsync();
            if (businessTripId.HasValue &&
                model.Id == 0 &&
                model.ClaimTypeId == businessTripId.Value)
            {
                var (isEligible, nextEligible) = await CheckBusinessTripEligibilityAsync(currentEmp.Id);

                if (!isEligible)
                {
                    ModelState.AddModelError(string.Empty,
                        $"You can only submit a Business Trip claim once every three years. " +
                        $"Next eligible date: {nextEligible:yyyy-MM-dd}.");

                    model.IsAdmin = currentEmp.IsAdmin || currentEmp.IsBoss;
                    model.CurrentUserId = currentEmp.Id;
                    ViewBag.ClaimTypeList = new SelectList(_context.ClaimTypes, "Id", "Name", model.ClaimTypeId);
                    return View(model.Id == 0 ? "Create" : "Edit", model);
                }
            }

            ClaimRequest request;

            if (model.Id > 0)
            {
                request = await _context.ClaimRequests
                    .Include(c => c.Employee)
                    .FirstOrDefaultAsync(c => c.Id == model.Id);

                if (request == null) return NotFound();

                // ✅ 编辑权限：
                // - 员工：只能改自己的
                // - Boss：只能操作 Sale/Acc 的（审批）
                // - Admin：不限制
                if (!currentEmp.IsAdmin)
                {
                    if (currentEmp.IsBoss)
                    {
                        if (request.Employee == null || !IsSaleOrAccDept(request.Employee.Department))
                            return Forbid();
                    }
                    else
                    {
                        if (request.EmployeeId != currentEmp.Id) return Forbid();
                    }
                }

                // ✅ 内容修改：
                // - 只有本人 或 Admin 可以改内容
                // - Boss 不改内容，只做审批状态/备注
                var canEditContent = currentEmp.IsAdmin || request.EmployeeId == currentEmp.Id;

                if (canEditContent)
                {
                    request.Amount = model.Amount;
                    request.Description = model.Description;
                    request.ClaimTypeId = model.ClaimTypeId;

                    if (file != null && file.Length > 0)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await file.CopyToAsync(memoryStream);
                            request.Receipt = Convert.ToBase64String(memoryStream.ToArray());
                        }
                    }
                }

                // ✅ 审批字段：Admin 或 Boss
                if (currentEmp.IsAdmin || currentEmp.IsBoss)
                {
                    request.Status = model.Status;
                    request.ApproverRemarks = model.ApproverRemarks;

                    if (string.Equals(model.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                    {
                        if (request.ApprovedDate == null)
                            request.ApprovedDate = DateTime.Now;
                    }
                }
                else
                {
                    if (request.Status != "Pending") request.Status = "Pending";
                }
            }
            else
            {
                // ✅ 新增：强制以登录者为准（防止篡改 EmployeeId）
                request = new ClaimRequest
                {
                    EmployeeId = currentEmp.Id,
                    RequestDate = DateTime.Now,
                    Amount = model.Amount,
                    Description = model.Description,
                    ClaimTypeId = model.ClaimTypeId,
                    Status = "Pending",
                    Receipt = ""
                };

                if (file != null && file.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await file.CopyToAsync(memoryStream);
                        request.Receipt = Convert.ToBase64String(memoryStream.ToArray());
                    }
                }

                _context.Add(request);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Claim request saved successfully.";
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // Business Trip helpers (原封不动保留)
        // =========================
        private async Task<int?> GetBusinessTripTypeIdAsync()
        {
            return await _context.ClaimTypes
                .Where(ct => ct.Name == BusinessTripTypeName)
                .Select(ct => (int?)ct.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<(bool isEligible, DateTime? nextEligible)> CheckBusinessTripEligibilityAsync(int employeeId)
        {
            var businessTripTypeId = await GetBusinessTripTypeIdAsync();
            if (businessTripTypeId == null) return (true, null);

            var lastApprovedClaim = await _context.ClaimRequests
                .Where(c => c.EmployeeId == employeeId &&
                            c.ClaimTypeId == businessTripTypeId.Value &&
                            c.Status == "Approved")
                .OrderByDescending(c => c.ApprovedDate ?? c.RequestDate)
                .FirstOrDefaultAsync();

            if (lastApprovedClaim == null) return (true, null);

            var baseDate = lastApprovedClaim.ApprovedDate ?? lastApprovedClaim.RequestDate;
            var nextEligible = baseDate.AddYears(3);

            if (DateTime.Now >= nextEligible) return (true, nextEligible);

            return (false, nextEligible);
        }

        private async Task UpdateEmployeeEligibilityAsync(Employee employee)
        {
            var (isEligible, nextEligible) = await CheckBusinessTripEligibilityAsync(employee.Id);
            employee.IsEligible = isEligible;
            employee.NextEligibleDate = nextEligible;
        }

        // =========================
        // DownloadReceipt (GET)
        // =========================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> DownloadReceipt(int id)
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            ViewBag.IsAdmin = currentEmp.IsAdmin;
            ViewBag.IsBoss = currentEmp.IsBoss;

            var claim = await _context.ClaimRequests
                .Include(c => c.Employee)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null) return NotFound();

            // ✅ 权限：
            // Admin：都可以
            // Boss：只能下载 Sale/Acc 的
            // 员工：只能下载自己的
            if (!currentEmp.IsAdmin)
            {
                if (currentEmp.IsBoss)
                {
                    if (claim.Employee == null || !IsSaleOrAccDept(claim.Employee.Department))
                        return Forbid();
                }
                else
                {
                    if (claim.EmployeeId != currentEmp.Id)
                        return Forbid();
                }
            }

            if (string.IsNullOrWhiteSpace(claim.Receipt))
                return NotFound("No document uploaded.");

            byte[] fileBytes;
            try
            {
                fileBytes = Convert.FromBase64String(claim.Receipt);
            }
            catch
            {
                return BadRequest("Invalid receipt data.");
            }

            var (contentType, ext) = GuessContentTypeAndExt(fileBytes);

            var employeeName = claim.Employee?.Name ?? "Employee";
            var downloadName = $"{employeeName}_Claim_{claim.Id}{ext}";

            return File(fileBytes, contentType, downloadName);
        }
    }
}
