using System;
using System.Security.Claims;
using System.Threading.Tasks;
using LeaveManagementSystem.Models;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagementSystem.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return null;

            return await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
        }

        private static bool LooksLikeHashedPassword(string? password)
        {
            return !string.IsNullOrWhiteSpace(password) && password.StartsWith("AQAAAA");
        }

        private static bool VerifyCurrentPassword(Employee emp, string inputCurrentPassword)
        {
            if (string.IsNullOrWhiteSpace(emp.Password)) return false;

            if (LooksLikeHashedPassword(emp.Password))
            {
                var hasher = new PasswordHasher<Employee>();
                var result = hasher.VerifyHashedPassword(emp, emp.Password, inputCurrentPassword);
                return result != PasswordVerificationResult.Failed;
            }

            return string.Equals(emp.Password, inputCurrentPassword);
        }

        // =========================
        // GET: /Profile/Index
        // =========================
        public async Task<IActionResult> Index()
        {
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // =========================
            // ✅ Top cards: AL Remaining / AL Used
            // AL Remaining 要包含 Carry Forward
            // =========================
            var annualRemainThisYear = Math.Max(0, emp.AnnualLeaveBalance);
            var annualCarry = Math.Max(0, emp.AnnualCarryForward);
            ViewBag.RemainingAnnual = annualRemainThisYear + annualCarry; // ✅ AL Remaining (include carry)
            ViewBag.TotalAnnual = emp.AnnualLeaveEntitlement;              // 你要显示年假 entitlement

            // ✅ AL Used 用你数据库的 double（支持 0.5）
            ViewBag.AnnualUsed = emp.AnnualLeaveUsed;

            // =========================
            // ✅ List section: 全部显示 Entitlement（不是 Balance/Remaining）
            // 下面这些 ViewBag 名字我保留原本格式，方便你 View 不用大改
            // =========================
            ViewBag.RemainingSick = emp.SickLeaveEntitlement; // ✅ 改成 entitlement
            ViewBag.TotalSick = emp.SickLeaveEntitlement;

            ViewBag.RemainingHospitalisation = emp.HospitalisationLeaveEntitlement; // ✅ entitlement
            ViewBag.TotalHospitalisation = emp.HospitalisationLeaveEntitlement;

            ViewBag.RemainingMaternity = emp.MaternityLeaveEntitlement; // ✅ entitlement
            ViewBag.TotalMaternity = emp.MaternityLeaveEntitlement;

            // 你原本没写 Paternity，我帮你补上（如果 View 有显示就直接有值）
            ViewBag.RemainingPaternity = emp.PaternityLeaveEntitlement; // ✅ entitlement
            ViewBag.TotalPaternity = emp.PaternityLeaveEntitlement;

            ViewBag.RemainingBereavement = emp.BereavementLeaveEntitlement; // ✅ entitlement
            ViewBag.TotalBereavement = emp.BereavementLeaveEntitlement;

            // Unpaid 你本来就是 No limit，保持
            ViewBag.RemainingUnpaid = "No limit";

            ViewBag.IsAdmin = emp.IsAdmin;

            return View(emp);
        }

        // =========================
        // POST: /Profile/Index (Admin update profile)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(Employee updated)
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.IsAdmin = currentEmp.IsAdmin;

            if (!currentEmp.IsAdmin)
            {
                return Unauthorized();
            }

            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == updated.Id);
            if (emp == null) return NotFound();

            emp.Name = updated.Name;
            emp.Email = updated.Email;
            emp.Department = updated.Department;
            emp.Role = updated.Role;
            emp.EmploymentType = updated.EmploymentType;
            emp.JoinDate = updated.JoinDate;

            // ✅ Admin 更新时：Annual balance 仍然是 entitlement - used
            // 但你现在有 carry forward，所以 balance 就只代表 this year balance（没问题）
            emp.AnnualLeaveEntitlement = updated.AnnualLeaveEntitlement;
            emp.AnnualLeaveUsed = updated.AnnualLeaveUsed;

            emp.AnnualLeaveBalance = Math.Max(0, updated.AnnualLeaveEntitlement - updated.AnnualLeaveUsed);

            // ✅ 如果你也想让 admin 能改 carry forward，可取消注释这一行：
            // emp.AnnualCarryForward = updated.AnnualCarryForward;

            await _context.SaveChangesAsync();

            TempData["ProfileSuccess"] = "Profile updated ✅";
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // POST: /Profile/ChangePassword
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
        {
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                TempData["PwError"] = "Please check the input (required / confirm if consistent / length).";
                return RedirectToAction(nameof(Index));
            }

            if (!VerifyCurrentPassword(emp, vm.CurrentPassword))
            {
                TempData["PwError"] = "The current password is incorrect.";
                return RedirectToAction(nameof(Index));
            }

            var hasher = new PasswordHasher<Employee>();
            emp.Password = hasher.HashPassword(emp, vm.NewPassword);

            await _context.SaveChangesAsync();

            TempData["PwSuccess"] = "Password has been updated ✅";
            return RedirectToAction(nameof(Index));
        }
    }
}
