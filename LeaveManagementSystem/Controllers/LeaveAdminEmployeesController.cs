using LeaveManagementSystem.Models;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LeaveManagementSystem.Controllers
{
    [Authorize]
    public class LeaveAdminEmployeesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LeaveAdminEmployeesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return null;
            return await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
        }

        private async Task<bool> CurrentUserIsAdminAsync()
        {
            var emp = await GetCurrentEmployeeAsync();
            return emp?.IsAdmin == true;
        }

        // ✅ 统一初始化：Used=0，Balance=Entitlement（不允许负数）
        private static void InitAllLeaveBalances(Employee e)
        {
            // Annual
            e.AnnualLeaveUsed = 0;
            e.AnnualLeaveBalance = Math.Max(0, e.AnnualLeaveEntitlement - e.AnnualLeaveUsed);

            // Sick
            e.SickLeaveUsed = 0;
            e.SickLeaveBalance = Math.Max(0, e.SickLeaveEntitlement - e.SickLeaveUsed);

            // Hospitalisation
            e.HospitalisationLeaveUsed = 0;
            e.HospitalisationLeaveBalance = Math.Max(0, e.HospitalisationLeaveEntitlement - e.HospitalisationLeaveUsed);

            // Maternity
            e.MaternityLeaveUsed = 0;
            e.MaternityLeaveBalance = Math.Max(0, e.MaternityLeaveEntitlement - e.MaternityLeaveUsed);

            // Paternity
            e.PaternityLeaveUsed = 0;
            e.PaternityLeaveBalance = Math.Max(0, e.PaternityLeaveEntitlement - e.PaternityLeaveUsed);

            // Bereavement
            e.BereavementLeaveUsed = 0;
            e.BereavementLeaveBalance = Math.Max(0, e.BereavementLeaveEntitlement - e.BereavementLeaveUsed);

            // Unpaid
            e.UnpaidLeaveUsed = 0;
            e.UnpaidLeaveBalance = Math.Max(0, e.UnpaidLeaveEntitlement - e.UnpaidLeaveUsed);
        }

        public async Task<IActionResult> Index()
        {
            if (!await CurrentUserIsAdminAsync()) return Unauthorized();

            var employees = await _context.Employees
                .OrderBy(e => e.Name)
                .ToListAsync();

            return View(employees);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!await CurrentUserIsAdminAsync()) return Unauthorized();

            // ✅ 给默认值（你页面要加 entitlement 输入框的话，这里会显示默认）
            var model = new Employee
            {
                EmploymentType = "Permanent",
                JoinDate = DateTime.Today,

                AnnualLeaveEntitlement = 12,
                SickLeaveEntitlement = 14
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,Email,Password,Department,EmploymentType,JoinDate,IsAdmin," +
                  "AnnualLeaveEntitlement,SickLeaveEntitlement,HospitalisationLeaveEntitlement," +
                  "MaternityLeaveEntitlement,PaternityLeaveEntitlement,BereavementLeaveEntitlement,UnpaidLeaveEntitlement")]
            Employee model,
            string? ConfirmPassword)
        {
            if (!await CurrentUserIsAdminAsync()) return Unauthorized();

            // 清理/默认值
            model.Name = (model.Name ?? "").Trim();
            model.Email = (model.Email ?? "").Trim();
            model.Department = (model.Department ?? "").Trim();
            model.EmploymentType = string.IsNullOrWhiteSpace(model.EmploymentType) ? "Permanent" : model.EmploymentType.Trim();
            model.JoinDate ??= DateTime.Today;

            // ✅ Role：你要的逻辑
            model.Role = model.IsAdmin ? "Administrator" : "Staff";

            // 基本验证
            if (string.IsNullOrWhiteSpace(model.Name))
                ModelState.AddModelError(nameof(model.Name), "Name is required.");

            if (string.IsNullOrWhiteSpace(model.Email))
                ModelState.AddModelError(nameof(model.Email), "Email is required.");

            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError(nameof(model.Password), "Password is required.");

            if (string.IsNullOrWhiteSpace(ConfirmPassword))
                ModelState.AddModelError("ConfirmPassword", "Confirm Password is required.");

            if (!string.IsNullOrWhiteSpace(model.Password)
                && !string.IsNullOrWhiteSpace(ConfirmPassword)
                && model.Password != ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Password and Confirm Password do not match.");
            }

            // Email 唯一
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                bool exists = await _context.Employees.AnyAsync(e => e.Email == model.Email);
                if (exists)
                    ModelState.AddModelError(nameof(model.Email), "This email already exists.");
            }

            // Entitlement 负数防呆（避免 UI 乱填）
            if (model.AnnualLeaveEntitlement < 0) model.AnnualLeaveEntitlement = 0;
            if (model.SickLeaveEntitlement < 0) model.SickLeaveEntitlement = 0;
            if (model.HospitalisationLeaveEntitlement < 0) model.HospitalisationLeaveEntitlement = 0;
            if (model.MaternityLeaveEntitlement < 0) model.MaternityLeaveEntitlement = 0;
            if (model.PaternityLeaveEntitlement < 0) model.PaternityLeaveEntitlement = 0;
            if (model.BereavementLeaveEntitlement < 0) model.BereavementLeaveEntitlement = 0;
            if (model.UnpaidLeaveEntitlement < 0) model.UnpaidLeaveEntitlement = 0;

            if (!ModelState.IsValid)
                return View(model);

            // ✅ 用 Identity PasswordHasher 生成 AQAAAA... 的 hash
            var hasher = new PasswordHasher<Employee>();
            model.Password = hasher.HashPassword(model, model.Password);

            // ✅ 初始化所有 leave 的 Used/Balance
            InitAllLeaveBalances(model);

            _context.Employees.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Employee added successfully.";
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // ✅ Unified Leave Wallet Edit
        // =========================

        [HttpGet]
        public async Task<IActionResult> EditLeaveWallet(int id)
        {
            if (!await CurrentUserIsAdminAsync()) return Unauthorized();

            var emp = await _context.Employees.FirstOrDefaultAsync(x => x.Id == id);
            if (emp == null) return NotFound();

            var vm = new EmployeeLeaveWalletEditVM
            {
                Id = emp.Id,
                Name = emp.Name,
                Email = emp.Email,
                Department = emp.Department,
                EmploymentType = emp.EmploymentType,
                JoinDate = emp.JoinDate,

                IsAdmin = emp.IsAdmin,
                Role = emp.Role,

                AnnualLeaveEntitlement = emp.AnnualLeaveEntitlement,
                AnnualLeaveUsed = emp.AnnualLeaveUsed,
                AnnualLeaveBalance = emp.AnnualLeaveBalance,

                SickLeaveEntitlement = emp.SickLeaveEntitlement,
                SickLeaveUsed = emp.SickLeaveUsed,
                SickLeaveBalance = emp.SickLeaveBalance,

                HospitalisationLeaveEntitlement = emp.HospitalisationLeaveEntitlement,
                HospitalisationLeaveUsed = emp.HospitalisationLeaveUsed,
                HospitalisationLeaveBalance = emp.HospitalisationLeaveBalance,

                MaternityLeaveEntitlement = emp.MaternityLeaveEntitlement,
                MaternityLeaveUsed = emp.MaternityLeaveUsed,
                MaternityLeaveBalance = emp.MaternityLeaveBalance,

                PaternityLeaveEntitlement = emp.PaternityLeaveEntitlement,
                PaternityLeaveUsed = emp.PaternityLeaveUsed,
                PaternityLeaveBalance = emp.PaternityLeaveBalance,

                BereavementLeaveEntitlement = emp.BereavementLeaveEntitlement,
                BereavementLeaveUsed = emp.BereavementLeaveUsed,
                BereavementLeaveBalance = emp.BereavementLeaveBalance,

                UnpaidLeaveEntitlement = emp.UnpaidLeaveEntitlement,
                UnpaidLeaveUsed = emp.UnpaidLeaveUsed,
                UnpaidLeaveBalance = emp.UnpaidLeaveBalance
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLeaveWallet(EmployeeLeaveWalletEditVM vm)
        {
            if (!await CurrentUserIsAdminAsync()) return Unauthorized();

            var emp = await _context.Employees.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (emp == null) return NotFound();

            if (!ModelState.IsValid)
            {
                vm.Name = emp.Name;
                vm.Email = emp.Email;
                vm.Department = emp.Department;
                vm.EmploymentType = emp.EmploymentType;
                vm.JoinDate = emp.JoinDate;
                return View(vm);
            }

            // ✅ Role 自动
            emp.IsAdmin = vm.IsAdmin;
            emp.Role = vm.IsAdmin ? "Administrator" : "Staff";

            // Annual
            emp.AnnualLeaveEntitlement = Math.Max(0, vm.AnnualLeaveEntitlement);
            emp.AnnualLeaveUsed = Math.Max(0, vm.AnnualLeaveUsed);
            emp.AnnualLeaveBalance = Math.Max(0, emp.AnnualLeaveEntitlement - emp.AnnualLeaveUsed);

            // Sick
            emp.SickLeaveEntitlement = Math.Max(0, vm.SickLeaveEntitlement);
            emp.SickLeaveUsed = Math.Max(0, vm.SickLeaveUsed);
            emp.SickLeaveBalance = Math.Max(0, emp.SickLeaveEntitlement - emp.SickLeaveUsed);

            // Hospitalisation
            emp.HospitalisationLeaveEntitlement = Math.Max(0, vm.HospitalisationLeaveEntitlement);
            emp.HospitalisationLeaveUsed = Math.Max(0, vm.HospitalisationLeaveUsed);
            emp.HospitalisationLeaveBalance = Math.Max(0, emp.HospitalisationLeaveEntitlement - emp.HospitalisationLeaveUsed);

            // Maternity
            emp.MaternityLeaveEntitlement = Math.Max(0, vm.MaternityLeaveEntitlement);
            emp.MaternityLeaveUsed = Math.Max(0, vm.MaternityLeaveUsed);
            emp.MaternityLeaveBalance = Math.Max(0, emp.MaternityLeaveEntitlement - emp.MaternityLeaveUsed);

            // Paternity
            emp.PaternityLeaveEntitlement = Math.Max(0, vm.PaternityLeaveEntitlement);
            emp.PaternityLeaveUsed = Math.Max(0, vm.PaternityLeaveUsed);
            emp.PaternityLeaveBalance = Math.Max(0, emp.PaternityLeaveEntitlement - emp.PaternityLeaveUsed);

            // Bereavement
            emp.BereavementLeaveEntitlement = Math.Max(0, vm.BereavementLeaveEntitlement);
            emp.BereavementLeaveUsed = Math.Max(0, vm.BereavementLeaveUsed);
            emp.BereavementLeaveBalance = Math.Max(0, emp.BereavementLeaveEntitlement - emp.BereavementLeaveUsed);

            // Unpaid
            emp.UnpaidLeaveEntitlement = Math.Max(0, vm.UnpaidLeaveEntitlement);
            emp.UnpaidLeaveUsed = Math.Max(0, vm.UnpaidLeaveUsed);
            emp.UnpaidLeaveBalance = Math.Max(0, emp.UnpaidLeaveEntitlement - emp.UnpaidLeaveUsed);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Leave wallet updated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
