using System.Diagnostics;
using System.Security.Claims; // ✅ NEW
using LeaveManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using LeaveManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using LeaveManagementSystem.Services;

namespace LeaveManagementSystem.Controllers
{
    public class AbstractHolidayDto
    {
        public string name { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public string location { get; set; } = string.Empty;
        public string date { get; set; } = string.Empty;
        public string date_year { get; set; } = string.Empty;
        public string date_month { get; set; } = string.Empty;
        public string date_day { get; set; } = string.Empty;
    }

    public class IdDto
    {
        public int Id { get; set; }
    }

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly FixedHolidayService _fixedHolidayService;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            IConfiguration config,
            FixedHolidayService fixedHolidayService
            )
        {
            _logger = logger;
            _context = context;
            _config = config;
            _fixedHolidayService = fixedHolidayService;
        }

        // =========================
        // ✅ NEW: 取当前登录员工
        // （避免你在Calendar/Claims那边一直重复写）
        // =========================
        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            // 你项目里常见是自定义 "UserId" claim
            var userIdStr = User.FindFirst("UserId")?.Value
                            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdStr, out int empId) || empId <= 0) return null;

            return await _context.Employees.FirstOrDefaultAsync(e => e.Id == empId);
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard(int? employeeFilterId, int? filterYear, int? filterMonth)
        {
            ViewBag.ActiveDashboard = "Leave";

            var now = DateTime.Today;

            var year = filterYear ?? now.Year;
            var month = filterMonth ?? now.Month;

            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var thisMonth = _context.LeaveRequests
                .Include(l => l.LeaveType)
                .Include(l => l.Employee)
                .Where(l => l.StartDate >= monthStart && l.StartDate < monthEnd);

            var vm = new DashboardViewModel
            {
                ThisMonthTotal = thisMonth.Count(),
                ThisMonthApproved = thisMonth.Count(l => l.Status == "Approved"),
                ThisMonthPending = thisMonth.Count(l => l.Status == "Pending"),
                ThisMonthRejected = thisMonth.Count(l => l.Status == "Rejected"),
                TotalPending = _context.LeaveRequests.Count(l => l.Status == "Pending"),

                ByLeaveType = thisMonth
                    .GroupBy(l => l.LeaveType!.Name)
                    .Select(g => new DashboardViewModel.LeaveTypeSummary
                    {
                        LeaveTypeName = g.Key!,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList(),

                EmployeeFilterId = employeeFilterId,
                FilterYear = year,
                FilterMonth = month
            };

            vm.EmployeeList = _context.Employees
                .OrderBy(e => e.Name)
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Name
                })
                .ToList();

            var years = _context.LeaveRequests
                .Select(l => l.StartDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            if (!years.Any())
            {
                years.Add(now.Year);
            }

            vm.AvailableYears = years;

            var historyQuery = _context.LeaveRequests
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l =>
                    l.StartDate >= monthStart &&
                    l.StartDate < monthEnd &&
                    (l.Status == "Approved" || l.Status == "Rejected")
                );

            if (employeeFilterId.HasValue)
            {
                historyQuery = historyQuery.Where(l => l.EmployeeId == employeeFilterId.Value);
            }

            vm.History = historyQuery
                .OrderBy(l => l.StartDate)
                .Select(l => new DashboardViewModel.LeaveHistoryItem
                {
                    Id = l.Id,
                    EmployeeName = l.Employee!.Name!,
                    LeaveTypeName = l.LeaveType!.Name!,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    TotalDays = l.TotalDays,
                    Status = l.Status!,
                    AttachmentPath = l.AttachmentPath
                })
                .ToList();

            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // =========================
        // ✅ Calendar Page
        // =========================
        [Authorize]
        public async Task<IActionResult> Calendar()
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return RedirectToAction("Login", "Account");

            // ✅ 给 _Layout 用，避免 ViewBag null 崩
            ViewBag.IsAdmin = currentEmp.IsAdmin;
            ViewBag.IsBoss = currentEmp.IsBoss;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SyncHolidays(int year = 2025, string? returnUrl = null)
        {
            var apiKey = _config["AbstractApi:HolidayKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                TempData["HolidaySyncMessage"] = "AbstractApi:Need to setup the HolidayKey ,please setup in appsettings.json 。";
                return RedirectToAction("Index", "Home");
            }

            int inserted = 0;
            var start = new DateTime(year, 1, 1);
            var end = new DateTime(year, 12, 31);

            using var client = new HttpClient();

            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var url =
                    $"https://holidays.abstractapi.com/v1/?" +
                    $"api_key={apiKey}&country=MY&year={year}&month={date.Month}&day={date.Day}";

                string json;
                try
                {
                    json = await client.GetStringAsync(url);
                }
                catch
                {
                    continue;
                }

                var holidays = JsonSerializer.Deserialize<List<AbstractHolidayDto>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new List<AbstractHolidayDto>();

                if (!holidays.Any())
                    continue;

                foreach (var h in holidays)
                {
                    var holidayDate = new DateTime(
                        int.Parse(h.date_year),
                        int.Parse(h.date_month),
                        int.Parse(h.date_day)
                    );

                    bool exists = await _context.PublicHolidays
                        .AnyAsync(x => x.Date == holidayDate && x.Name == h.name);

                    if (exists)
                        continue;

                    _context.PublicHolidays.Add(new PublicHoliday
                    {
                        Date = holidayDate,
                        Name = h.name,
                        Type = h.type,
                        CountryCode = "MY",
                        Location = h.location,
                        Year = year
                    });

                    inserted++;
                }

                await _context.SaveChangesAsync();
            }

            TempData["HolidaySyncMessage"] = $"Complate {year} holiday，New {inserted} record。";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Calendar", "Home");
        }

        // =========================
        // ✅ Calendar Events API
        // 重点：Boss 只看 Sale/Acc 的 Leave
        // =========================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetLeaveEvents()
        {
            var currentEmp = await GetCurrentEmployeeAsync();
            if (currentEmp == null) return Unauthorized();

            // ✅ 给 _Layout 用（有时候前端会先请求事件再渲染）
            ViewBag.IsAdmin = currentEmp.IsAdmin;
            ViewBag.IsBoss = currentEmp.IsBoss;

            // ✅ Leave（Approved）
            var leaveQuery = _context.LeaveRequests
                .AsNoTracking()
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.Status == "Approved");

            // ✅ Boss：只看 Sale / Acc 的员工申请
            // Admin：不限制
            if (currentEmp.IsBoss && !currentEmp.IsAdmin)
            {
                leaveQuery = leaveQuery.Where(l =>
                    l.Employee != null &&
                    l.Employee.Department != null &&
                    (
                        l.Employee.Department.Trim().ToLower() == "sale" ||
                        l.Employee.Department.Trim().ToLower() == "sales" ||
                        l.Employee.Department.Trim().ToLower() == "acc" ||
                        l.Employee.Department.Trim().ToLower() == "account" ||
                        l.Employee.Department.Trim().ToLower() == "accounts" ||
                        l.Employee.Department.Trim().ToLower() == "accounting"
                    )
                );
            }

            var leaveRequests = await leaveQuery.ToListAsync();

            // ✅ Public Holidays（所有人可见）
            var holidays = await _context.PublicHolidays.AsNoTracking().ToListAsync();

            string GetLeaveColor(string? leaveTypeName)
            {
                var key = (leaveTypeName ?? "").Trim().ToLowerInvariant();

                return key switch
                {
                    "annual leave" => "#A88F5E",          // muted caramel
                    "sick leave" => "#6F8FAE",            // dusty steel blue
                    "bereavement leave" => "#7F7B73",     // warm slate
                    "hospitalisation leave" => "#7B79A6", // muted violet slate
                    "maternity leave" => "#B07C8C",       // muted rose
                    "emergency leave" => "#B06F4E",       // muted amber
                    "paternity leave" => "#6F9B88",       // muted sage teal
                    _ => "#8B857C"                        // neutral fallback
                };
            }

            const string holidayBg = "#9C5A63";
            const string holidayText = "#FFFFFF";

            var leaveEvents = leaveRequests.Select(l =>
            {
                var leaveTypeName = l.LeaveType?.Name ?? "Leave";
                var bg = GetLeaveColor(leaveTypeName);

                return new
                {
                    id = $"leave-{l.Id}",
                    title = (l.Employee?.Name ?? "Unknown") + " - " + leaveTypeName,
                    start = l.StartDate.ToString("yyyy-MM-dd"),
                    end = l.EndDate.AddDays(1).ToString("yyyy-MM-dd"),

                    url = Url.Action("Details", "LeaveRequests", new { id = l.Id }),

                    isPublicHoliday = false,

                    holidayType = (string?)null,
                    holidayLocation = (string?)null,

                    backgroundColor = bg,
                    borderColor = bg,
                    textColor = "#FFFFFF",

                    leaveTypeName = leaveTypeName
                };
            });

            var holidayEvents = holidays.Select(h => new
            {
                id = $"holiday-{h.Id}",
                title = h.Name,
                start = h.Date.ToString("yyyy-MM-dd"),
                end = h.Date.AddDays(1).ToString("yyyy-MM-dd"),

                url = (string?)null,

                isPublicHoliday = true,
                holidayType = h.Type,
                holidayLocation = h.Location,

                backgroundColor = holidayBg,
                borderColor = holidayBg,
                textColor = holidayText,

                leaveTypeName = "Public Holiday"
            });

            var allEvents = leaveEvents.Concat(holidayEvents).ToList();
            return Json(allEvents);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddPublicHoliday([FromBody] AddPublicHolidayViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var date = model.Date.Date;

            bool exists = await _context.PublicHolidays
                .AnyAsync(x => x.Date == date && x.Name == model.Name);

            if (exists)
            {
                return Conflict(new { message = "This holiday already exists." });
            }

            var holiday = new PublicHoliday
            {
                Date = date,
                Name = model.Name,
                Type = string.IsNullOrEmpty(model.Type) ? "public" : model.Type,
                CountryCode = "MY",
                Location = string.IsNullOrEmpty(model.Location) ? "Malaysia" : model.Location,
                Year = date.Year
            };

            _context.PublicHolidays.Add(holiday);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                id = holiday.Id,
                name = holiday.Name,
                date = holiday.Date.ToString("yyyy-MM-dd")
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePublicHoliday([FromBody] EditPublicHolidayViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid data." });
            }

            var holiday = await _context.PublicHolidays.FirstOrDefaultAsync(x => x.Id == model.Id);
            if (holiday == null)
            {
                return NotFound(new { message = "Holiday not found." });
            }

            var date = model.Date.Date;

            bool exists = await _context.PublicHolidays.AnyAsync(x =>
                x.Id != model.Id &&
                x.Date == date &&
                x.Name == model.Name);

            if (exists)
            {
                return Conflict(new { message = "Another holiday with the same name and date already exists." });
            }

            holiday.Name = model.Name.Trim();
            holiday.Date = date;
            holiday.Type = string.IsNullOrWhiteSpace(model.Type) ? "public" : model.Type.Trim();
            holiday.Location = string.IsNullOrWhiteSpace(model.Location) ? "Malaysia" : model.Location.Trim();
            holiday.Year = date.Year;

            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePublicHoliday([FromBody] IdDto dto)
        {
            if (dto == null || dto.Id <= 0)
            {
                return BadRequest(new { message = "Invalid holiday id." });
            }

            var holiday = await _context.PublicHolidays.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (holiday == null)
            {
                return NotFound(new { message = "Holiday not found." });
            }

            _context.PublicHolidays.Remove(holiday);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [Authorize]
        public async Task<IActionResult> ClaimDashboard(int? year, int? month, int? employeeId)
        {
            var now = DateTime.Now;
            int selectedYear = year ?? now.Year;
            int selectedMonth = month ?? now.Month;

            var startDate = new DateTime(selectedYear, selectedMonth, 1);
            var endDate = startDate.AddMonths(1);

            var allQuery = _context.ClaimRequests
                .Include(c => c.Employee)
                .Include(c => c.ClaimType)
                .Where(c => c.RequestDate >= startDate && c.RequestDate < endDate)
                .Where(c => c.Status != "Cancelled");

            if (employeeId.HasValue && employeeId.Value > 0)
            {
                allQuery = allQuery.Where(c => c.EmployeeId == employeeId.Value);
            }

            var allList = await allQuery.ToListAsync();

            var historyList = allList
                .Where(c => c.Status == "Approved" || c.Status == "Rejected")
                .ToList();

            var eligibilityByEmployee = new Dictionary<int, (bool IsEligible, DateTime? NextEligibleDate)>();

            var employeesInResult = allList
                .Where(c => c.Employee != null)
                .Select(c => c.Employee!)
                .GroupBy(e => e.Id)
                .Select(g => g.First())
                .ToList();

            foreach (var emp in employeesInResult)
            {
                var lastApprovedBusinessTrip = await _context.ClaimRequests
                    .Include(c => c.ClaimType)
                    .Where(c =>
                        c.EmployeeId == emp.Id &&
                        c.Status == "Approved" &&
                        c.ClaimType != null &&
                        c.ClaimType.Name == "Business Trip")
                    .OrderByDescending(c => c.ApprovedDate)
                    .FirstOrDefaultAsync();

                DateTime cycleStart = lastApprovedBusinessTrip?.ApprovedDate
                                      ?? (emp.JoinDate ?? now);

                DateTime nextEligible = cycleStart.AddYears(3);
                bool isEligibleNow = now >= nextEligible;

                eligibilityByEmployee[emp.Id] = (isEligibleNow, nextEligible);
            }

            var vm = new ClaimDashboardViewModel
            {
                Year = selectedYear,
                Month = selectedMonth,
                SelectedEmployeeId = employeeId,

                TotalClaims = allList.Count,
                ApprovedCount = allList.Count(c => c.Status == "Approved"),
                PendingCount = allList.Count(c => c.Status == "Pending"),
                RejectedCount = allList.Count(c => c.Status == "Rejected"),

                ClaimsByType = allList
                    .GroupBy(c => c.ClaimType?.Name ?? "Unknown")
                    .Select(g => new ClaimTypeSummary
                    {
                        ClaimTypeName = g.Key,
                        Count = g.Count(),
                        TotalAmount = g.Sum(x => x.Amount)
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList(),

                ClaimHistory = historyList
                    .OrderByDescending(c => c.RequestDate)
                    .Select(c =>
                    {
                        bool isBusinessTrip = string.Equals(
                            c.ClaimType?.Name,
                            "Business Trip",
                            StringComparison.OrdinalIgnoreCase);

                        bool isEligible = true;
                        DateTime? nextEligibleDate = null;

                        if (isBusinessTrip &&
                            eligibilityByEmployee.TryGetValue(c.EmployeeId, out var info))
                        {
                            isEligible = info.IsEligible;
                            nextEligibleDate = info.NextEligibleDate;
                        }

                        return new ClaimHistoryRow
                        {
                            Id = c.Id,
                            EmployeeName = c.Employee?.Name ?? "Unknown",
                            ClaimTypeName = c.ClaimType?.Name ?? "Unknown",
                            Amount = c.Amount,
                            Status = c.Status,
                            RequestDate = c.RequestDate,

                            Receipt = c.Receipt,

                            EmployeeIsEligible = isEligible,
                            EmployeeNextEligibleDate = nextEligibleDate
                        };
                    })
                    .ToList()
            };

            ViewBag.ActiveDashboard = "Claim";

            ViewBag.EmployeeList = _context.Employees
                .OrderBy(e => e.Name)
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Name
                })
                .ToList();

            return View(vm);
        }
    }
}
