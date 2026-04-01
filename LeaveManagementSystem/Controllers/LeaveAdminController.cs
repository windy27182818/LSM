using LeaveManagementSystem.Models;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace LeaveManagementSystem.Controllers
{
    [Authorize]
    public class LeaveAdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LeaveAdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<bool> CurrentUserIsAdminAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return false;

            var emp = await _context.Employees
                .FirstOrDefaultAsync(e => e.Email == email);

            return emp != null && emp.IsAdmin;
        }

        // GET: /LeaveAdmin
        public async Task<IActionResult> Index()
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            var employees = await _context.Employees.ToListAsync();

            var model = employees.Select(e => new LeaveAdminViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Email = e.Email,
                Department = e.Department,
                EmploymentType = e.EmploymentType,
                JoinDate = e.JoinDate,

                AnnualLeaveEntitlement = e.AnnualLeaveEntitlement,
                AnnualLeaveUsed = e.AnnualLeaveUsed,
                AnnualLeaveBalance = e.AnnualLeaveBalance,
                AnnualCarryForward = e.AnnualCarryForward,

                SickLeaveEntitlement = e.SickLeaveEntitlement,
                SickLeaveUsed = e.SickLeaveUsed,
                SickLeaveBalance = e.SickLeaveBalance,

                HospitalisationLeaveEntitlement = e.HospitalisationLeaveEntitlement,
                HospitalisationLeaveUsed = e.HospitalisationLeaveUsed,
                HospitalisationLeaveBalance = e.HospitalisationLeaveBalance,

                MaternityLeaveEntitlement = e.MaternityLeaveEntitlement,
                MaternityLeaveUsed = e.MaternityLeaveUsed,
                MaternityLeaveBalance = e.MaternityLeaveBalance,

                // ✅ 加上 Paternity（你漏了这个）
                PaternityLeaveEntitlement = e.PaternityLeaveEntitlement,
                PaternityLeaveUsed = e.PaternityLeaveUsed,
                PaternityLeaveBalance = e.PaternityLeaveBalance,

                BereavementLeaveEntitlement = e.BereavementLeaveEntitlement,
                BereavementLeaveUsed = e.BereavementLeaveUsed,
                BereavementLeaveBalance = e.BereavementLeaveBalance,

                UnpaidLeaveEntitlement = e.UnpaidLeaveEntitlement,
                UnpaidLeaveUsed = e.UnpaidLeaveUsed,
                UnpaidLeaveBalance = e.UnpaidLeaveBalance,

                IsAdmin = e.IsAdmin
            }).ToList();

            return View(model);
        }

        // GET: LeaveAdmin/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            var e = await _context.Employees.FindAsync(id);
            if (e == null)
            {
                return NotFound();
            }

            var model = new LeaveAdminViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Email = e.Email,
                Department = e.Department,
                EmploymentType = e.EmploymentType,
                JoinDate = e.JoinDate,

                AnnualLeaveEntitlement = e.AnnualLeaveEntitlement,
                AnnualLeaveUsed = e.AnnualLeaveUsed,
                AnnualLeaveBalance = e.AnnualLeaveBalance,

                SickLeaveEntitlement = e.SickLeaveEntitlement,
                SickLeaveUsed = e.SickLeaveUsed,
                SickLeaveBalance = e.SickLeaveBalance,

                HospitalisationLeaveEntitlement = e.HospitalisationLeaveEntitlement,
                HospitalisationLeaveUsed = e.HospitalisationLeaveUsed,
                HospitalisationLeaveBalance = e.HospitalisationLeaveBalance,

                MaternityLeaveEntitlement = e.MaternityLeaveEntitlement,
                MaternityLeaveUsed = e.MaternityLeaveUsed,
                MaternityLeaveBalance = e.MaternityLeaveBalance,

                PaternityLeaveEntitlement = e.PaternityLeaveEntitlement,
                PaternityLeaveUsed = e.PaternityLeaveUsed,
                PaternityLeaveBalance = e.PaternityLeaveBalance,

                BereavementLeaveEntitlement = e.BereavementLeaveEntitlement,
                BereavementLeaveUsed = e.BereavementLeaveUsed,
                BereavementLeaveBalance = e.BereavementLeaveBalance,

                UnpaidLeaveEntitlement = e.UnpaidLeaveEntitlement,
                UnpaidLeaveUsed = e.UnpaidLeaveUsed,
                UnpaidLeaveBalance = e.UnpaidLeaveBalance,

                IsAdmin = e.IsAdmin
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LeaveAdminViewModel model)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var e = await _context.Employees.FindAsync(id);
            if (e == null)
            {
                return NotFound();
            }

            e.EmploymentType = model.EmploymentType!;
            e.JoinDate = model.JoinDate;

            // 年假
            e.AnnualLeaveEntitlement = model.AnnualLeaveEntitlement;
            e.AnnualLeaveUsed = model.AnnualLeaveUsed;
            e.AnnualLeaveBalance = model.AnnualLeaveBalance != 0
                ? model.AnnualLeaveBalance
                : (model.AnnualLeaveEntitlement - model.AnnualLeaveUsed);

            // 病假
            e.SickLeaveEntitlement = model.SickLeaveEntitlement;
            e.SickLeaveUsed = model.SickLeaveUsed;
            e.SickLeaveBalance = model.SickLeaveBalance != 0
                ? model.SickLeaveBalance
                : (model.SickLeaveEntitlement - model.SickLeaveUsed);

            // 住院假
            e.HospitalisationLeaveEntitlement = model.HospitalisationLeaveEntitlement;
            e.HospitalisationLeaveUsed = model.HospitalisationLeaveUsed;
            e.HospitalisationLeaveBalance = model.HospitalisationLeaveBalance != 0
                ? model.HospitalisationLeaveBalance
                : (model.HospitalisationLeaveEntitlement - model.HospitalisationLeaveUsed);

            // 产假
            e.MaternityLeaveEntitlement = model.MaternityLeaveEntitlement;
            e.MaternityLeaveUsed = model.MaternityLeaveUsed;
            e.MaternityLeaveBalance = model.MaternityLeaveBalance != 0
                ? model.MaternityLeaveBalance
                : (model.MaternityLeaveEntitlement - model.MaternityLeaveUsed);

            // 陪产假 Paternity
            e.PaternityLeaveEntitlement = model.PaternityLeaveEntitlement;
            e.PaternityLeaveUsed = model.PaternityLeaveUsed;

            var pBal = model.PaternityLeaveEntitlement - model.PaternityLeaveUsed;
            e.PaternityLeaveBalance = pBal < 0 ? 0 : pBal;

            // 丧假
            e.BereavementLeaveEntitlement = model.BereavementLeaveEntitlement;
            e.BereavementLeaveUsed = model.BereavementLeaveUsed;
            e.BereavementLeaveBalance = model.BereavementLeaveBalance != 0
                ? model.BereavementLeaveBalance
                : (model.BereavementLeaveEntitlement - model.BereavementLeaveUsed);

            // 无薪假（通常无配额，这里逻辑你之后可以调整）
            e.UnpaidLeaveEntitlement = model.UnpaidLeaveEntitlement;
            e.UnpaidLeaveUsed = model.UnpaidLeaveUsed;
            e.UnpaidLeaveBalance = model.UnpaidLeaveBalance != 0
                ? model.UnpaidLeaveBalance
                : (model.UnpaidLeaveEntitlement - model.UnpaidLeaveUsed);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: LeaveAdmin/EditAnnual/5
        public async Task<IActionResult> EditAnnual(int id)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            var model = new LeaveAdminViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Email = e.Email,
                Department = e.Department,
                EmploymentType = e.EmploymentType,
                JoinDate = e.JoinDate,

                AnnualLeaveEntitlement = e.AnnualLeaveEntitlement,
                AnnualLeaveUsed = e.AnnualLeaveUsed,
                AnnualLeaveBalance = e.AnnualLeaveBalance,

                IsAdmin = e.IsAdmin
            };

            return View("EditAnnual", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAnnual(int id, LeaveAdminViewModel model)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            if (id != model.Id) return BadRequest();

            if (!ModelState.IsValid)
                return View("EditAnnual", model);

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            e.AnnualLeaveEntitlement = model.AnnualLeaveEntitlement;
            e.AnnualLeaveUsed = model.AnnualLeaveUsed;
            e.AnnualLeaveBalance = model.AnnualLeaveEntitlement - model.AnnualLeaveUsed;
            if (e.AnnualLeaveBalance < 0) e.AnnualLeaveBalance = 0;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> EditSick(int id)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            var model = new LeaveAdminViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Email = e.Email,
                Department = e.Department,
                EmploymentType = e.EmploymentType,
                JoinDate = e.JoinDate,

                SickLeaveEntitlement = e.SickLeaveEntitlement,
                SickLeaveUsed = e.SickLeaveUsed,
                SickLeaveBalance = e.SickLeaveBalance,

                IsAdmin = e.IsAdmin
            };

            return View("EditSick", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSick(int id, LeaveAdminViewModel model)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View("EditSick", model);

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            e.SickLeaveEntitlement = model.SickLeaveEntitlement;
            e.SickLeaveUsed = model.SickLeaveUsed;

            var bal = model.SickLeaveEntitlement - model.SickLeaveUsed;
            e.SickLeaveBalance = bal < 0 ? 0 : bal;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> EditHospitalisation(int id)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            var model = new LeaveAdminViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Email = e.Email,
                Department = e.Department,
                EmploymentType = e.EmploymentType,
                JoinDate = e.JoinDate,

                HospitalisationLeaveEntitlement = e.HospitalisationLeaveEntitlement,
                HospitalisationLeaveUsed = e.HospitalisationLeaveUsed,
                HospitalisationLeaveBalance = e.HospitalisationLeaveBalance,

                IsAdmin = e.IsAdmin
            };

            return View("EditHospitalisation", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditHospitalisation(int id, LeaveAdminViewModel model)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View("EditHospitalisation", model);

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            e.HospitalisationLeaveEntitlement = model.HospitalisationLeaveEntitlement;
            e.HospitalisationLeaveUsed = model.HospitalisationLeaveUsed;

            var bal = model.HospitalisationLeaveEntitlement - model.HospitalisationLeaveUsed;
            e.HospitalisationLeaveBalance = bal < 0 ? 0 : bal;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> EditMaternity(int id)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            var model = new LeaveAdminViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Email = e.Email,
                Department = e.Department,
                EmploymentType = e.EmploymentType,
                JoinDate = e.JoinDate,

                MaternityLeaveEntitlement = e.MaternityLeaveEntitlement,
                MaternityLeaveUsed = e.MaternityLeaveUsed,
                MaternityLeaveBalance = e.MaternityLeaveBalance,

                IsAdmin = e.IsAdmin
            };

            return View("EditMaternity", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMaternity(int id, LeaveAdminViewModel model)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View("EditMaternity", model);

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            e.MaternityLeaveEntitlement = model.MaternityLeaveEntitlement;
            e.MaternityLeaveUsed = model.MaternityLeaveUsed;

            var bal = model.MaternityLeaveEntitlement - model.MaternityLeaveUsed;
            e.MaternityLeaveBalance = bal < 0 ? 0 : bal;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        // GET: LeaveAdmin/EditPaternity/5
        // 用于显示编辑页面
        public async Task<IActionResult> EditPaternity(int id)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            // 将 Employee 数据转换为 ViewModel 以便在视图中显示
            var model = new LeaveAdminViewModel
            {
                Id = employee.Id,
                Name = employee.Name, // 只需要显示名字，不需要编辑
                Email = employee.Email,

                // 绑定 Paternity 相关字段
                PaternityLeaveEntitlement = employee.PaternityLeaveEntitlement,
                PaternityLeaveUsed = employee.PaternityLeaveUsed,
                PaternityLeaveBalance = employee.PaternityLeaveBalance
            };

            return View(model);
        }

        // POST: LeaveAdmin/EditPaternity/5
        // 用于提交保存
        [HttpPost]
        [ValidateAntiForgeryToken] // 建议加上这个防止 CSRF 攻击
        public async Task<IActionResult> EditPaternity(int id, LeaveAdminViewModel model)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            if (id != model.Id) return BadRequest();

            // 如果数据验证失败（例如输入了负数），重新返回视图
            if (!ModelState.IsValid) return View(model);

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            // 1. 更新配额和已用数量
            e.PaternityLeaveEntitlement = model.PaternityLeaveEntitlement;
            e.PaternityLeaveUsed = model.PaternityLeaveUsed;

            // 2. 自动计算余额 (Entitlement - Used)
            var bal = model.PaternityLeaveEntitlement - model.PaternityLeaveUsed;

            // 3. 防止余额为负数
            e.PaternityLeaveBalance = bal < 0 ? 0 : bal;

            await _context.SaveChangesAsync();

            // 保存成功后返回列表页
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> EditBereavement(int id)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            var model = new LeaveAdminViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Email = e.Email,
                Department = e.Department,
                EmploymentType = e.EmploymentType,
                JoinDate = e.JoinDate,

                BereavementLeaveEntitlement = e.BereavementLeaveEntitlement,
                BereavementLeaveUsed = e.BereavementLeaveUsed,
                BereavementLeaveBalance = e.BereavementLeaveBalance,

                IsAdmin = e.IsAdmin
            };

            return View("EditBereavement", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBereavement(int id, LeaveAdminViewModel model)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View("EditBereavement", model);

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            e.BereavementLeaveEntitlement = model.BereavementLeaveEntitlement;
            e.BereavementLeaveUsed = model.BereavementLeaveUsed;

            var bal = model.BereavementLeaveEntitlement - model.BereavementLeaveUsed;
            e.BereavementLeaveBalance = bal < 0 ? 0 : bal;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> EditUnpaid(int id)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            var model = new LeaveAdminViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Email = e.Email,
                Department = e.Department,
                EmploymentType = e.EmploymentType,
                JoinDate = e.JoinDate,

                UnpaidLeaveEntitlement = e.UnpaidLeaveEntitlement,
                UnpaidLeaveUsed = e.UnpaidLeaveUsed,
                UnpaidLeaveBalance = e.UnpaidLeaveBalance,

                IsAdmin = e.IsAdmin
            };

            return View("EditUnpaid", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUnpaid(int id, LeaveAdminViewModel model)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View("EditUnpaid", model);

            var e = await _context.Employees.FindAsync(id);
            if (e == null) return NotFound();

            e.UnpaidLeaveEntitlement = model.UnpaidLeaveEntitlement;
            e.UnpaidLeaveUsed = model.UnpaidLeaveUsed;

            var bal = model.UnpaidLeaveEntitlement - model.UnpaidLeaveUsed;
            e.UnpaidLeaveBalance = bal < 0 ? 0 : bal;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: LeaveAdmin/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /LeaveAdmin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,Email,Department,EmploymentType,JoinDate")] Employee employee)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            ModelState.Remove("Password");
            ModelState.Remove("Role");

            if (!ModelState.IsValid)
            {
                return View(employee);
            }

            // Default values
            employee.Password = "123456";
            employee.Role = "Staff";

            // 年假
            employee.AnnualLeaveEntitlement = 12.0;
            employee.AnnualLeaveUsed = 0.0;
            employee.AnnualLeaveBalance = 12.0;

            // 病假
            employee.SickLeaveEntitlement = 14;
            employee.SickLeaveUsed = 0;
            employee.SickLeaveBalance = 14;

            // 住院假（你可以自己改默认值）
            employee.HospitalisationLeaveEntitlement = 0.0;
            employee.HospitalisationLeaveUsed = 0.0;
            employee.HospitalisationLeaveBalance = 0.0;

            // 产假（通常只给女性，这里先设 0，需要时再手动改）
            employee.MaternityLeaveEntitlement = 0.0;
            employee.MaternityLeaveUsed = 0.0;
            employee.MaternityLeaveBalance = 0.0;

            // Paternity
            employee.PaternityLeaveEntitlement = 0.0;
            employee.PaternityLeaveUsed = 0.0;
            employee.PaternityLeaveBalance = 0.0;

            // 丧假
            employee.BereavementLeaveEntitlement = 0.0;
            employee.BereavementLeaveUsed = 0.0;
            employee.BereavementLeaveBalance = 0.0;

            // 无薪假
            employee.UnpaidLeaveEntitlement = 0.0;
            employee.UnpaidLeaveUsed = 0.0;
            employee.UnpaidLeaveBalance = 0.0;

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await CurrentUserIsAdminAsync())
                return Unauthorized();

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }

            var currentEmail = User.FindFirstValue(ClaimTypes.Email);
            if (!string.IsNullOrEmpty(currentEmail) &&
                string.Equals(employee.Email, currentEmail, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ApprovedList(string? q)
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
            if (emp == null || !emp.IsAdmin) return Unauthorized();

            var allowedStatuses = new[] { "Pending", "Approved", "Rejected", "Cancelled" };

            var query = _context.LeaveRequests
                .Include(x => x.Employee)
                .Include(x => x.LeaveType)
                .Where(x => allowedStatuses.Contains(x.Status))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                // ✅ 拆分关键词：空格、/、,、;、| 都能分
                var tokens = q.Trim()
                    .Split(new[] { ' ', '/', ',', ';', '|', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // ✅ 每个 token 都要命中（AND）
                foreach (var rawToken in tokens)
                {
                    var token = rawToken.ToLowerInvariant();

                    // ✅ 日期 token
                    bool isDate = DateTime.TryParseExact(rawToken,
                        new[] { "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "d/M/yyyy", "d-M-yyyy" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime parsedDate);

                    if (isDate)
                    {
                        query = query.Where(x =>
                            x.StartDate.Date == parsedDate.Date ||
                            x.EndDate.Date == parsedDate.Date ||
                            (x.ApprovedDate.HasValue && x.ApprovedDate.Value.Date == parsedDate.Date) ||
                            x.CreatedAt.Date == parsedDate.Date
                        );
                    }
                    else
                    {
                        query = query.Where(x =>
                            (x.Employee != null &&
                                (((x.Employee.Name ?? "").ToLower().Contains(token)) ||
                                 ((x.Employee.Email ?? "").ToLower().Contains(token)))) ||

                            (x.LeaveType != null && (x.LeaveType.Name ?? "").ToLower().Contains(token)) ||

                            ((x.Status ?? "").ToLower().Contains(token)) ||
                            ((x.Reason ?? "").ToLower().Contains(token)) ||
                            ((x.ApproverRemark ?? "").ToLower().Contains(token))
                        );
                    }
                }
            }

            var list = await query
                .OrderByDescending(x => x.ApprovedDate ?? x.CreatedAt)
                .ToListAsync();

            ViewBag.Q = q;
            return View(list);
        }
    }
}
