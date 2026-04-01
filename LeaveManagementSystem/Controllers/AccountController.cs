using LeaveManagementSystem.Models;
using LeaveManagementSystem.Services;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LeaveManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static bool LooksLikeHashedPassword(string? password)
        {
            return !string.IsNullOrWhiteSpace(password) && password.StartsWith("AQAAAA");
        }

        private static bool VerifyPassword(Employee user, string inputPassword)
        {
            if (string.IsNullOrWhiteSpace(user.Password)) return false;

            if (LooksLikeHashedPassword(user.Password))
            {
                var hasher = new PasswordHasher<Employee>();
                var result = hasher.VerifyHashedPassword(user, user.Password, inputPassword);
                return result != PasswordVerificationResult.Failed;
            }

            return string.Equals(user.Password, inputPassword);
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null, string? apn = null, string? devicetype = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            ViewData["APN"] = apn;
            ViewData["DeviceType"] = devicetype;

            return View(new LoginViewModel());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
    LoginViewModel model,
    string? returnUrl = null,
    string? APN = null,
    string? DeviceType = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Employees.FirstOrDefaultAsync(e => e.Email == model.Email);

            if (user == null || !VerifyPassword(user, model.Password))
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            bool tokenChanged = false;

            if (!string.IsNullOrWhiteSpace(APN))
            {
                var newApn = APN.Trim();
                if (!string.Equals(user.APN, newApn, StringComparison.Ordinal))
                {
                    user.APN = newApn;
                    tokenChanged = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(DeviceType))
            {
                var newType = DeviceType.Trim().ToLowerInvariant();
                if (!string.Equals(user.DeviceType, newType, StringComparison.Ordinal))
                {
                    user.DeviceType = newType;
                    tokenChanged = true;
                }
            }

            if (!LooksLikeHashedPassword(user.Password))
            {
                var hasher = new PasswordHasher<Employee>();
                user.Password = hasher.HashPassword(user, model.Password);
                tokenChanged = true;
            }

            if (tokenChanged)
            {
                await _context.SaveChangesAsync();
            }

            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Name ?? user.Email ?? "Unknown"),
                    new Claim(ClaimTypes.Email, user.Email!),
                    new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "Employee"),
                    new Claim("UserId", user.Id.ToString()),
                    new Claim("IsAdmin", user.IsAdmin.ToString())
                };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddMinutes(20),
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "LeaveRequests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public async Task<IActionResult> AutoLogout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}
