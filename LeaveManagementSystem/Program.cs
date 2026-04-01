using LeaveManagementSystem.Helpers;
using LeaveManagementSystem.Models;
using LeaveManagementSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
// ✅ ADD
using System.Net;
using System.Security.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Configure the connection string for the database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
    });

// Add services to the container
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new NoCacheAttribute());
});

// Add HolidaySyncService with HttpClient
builder.Services.AddHttpClient<HolidaySyncService>();

builder.Services.AddScoped<FixedHolidayService>();

builder.Services.Configure<LeaveManagementSystem.Services.SmtpSettings>(
    builder.Configuration.GetSection("SmtpSettings"));

builder.Services.AddTransient<LeaveManagementSystem.Services.IEmailNotificationService,
    LeaveManagementSystem.Services.EmailNotificationService>();

builder.Services.AddTransient<LeaveManagementSystem.Services.INotificationDispatcher,
    LeaveManagementSystem.Services.NotificationDispatcher>();


// ✅ General HttpClientFactory (keep)
builder.Services.AddHttpClient();

builder.Services.AddHttpClient<NotificationPushService>();
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\LeaveManagementSystem\keys"))
    .SetApplicationName("JDS-LMS");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var fixedHolidayService = scope.ServiceProvider.GetRequiredService<FixedHolidayService>();
    var currentYear = DateTime.Now.Year;

    /*fixedHolidayService
        .EnsureYearlyFixedHolidaysAsync(currentYear)
        .GetAwaiter()
        .GetResult();*/
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// ✅ 1) 访问 /LMS 先去登录页
app.MapControllerRoute(
    name: "root",
    pattern: "",
    defaults: new { controller = "Account", action = "Login" });

// ✅ 2) PushTest 快捷路由：/LMS/PushTest
app.MapControllerRoute(
    name: "pushtest",
    pattern: "PushTest",
    defaults: new { controller = "PushTest", action = "Index" });

// ✅ 3) 标准 MVC 路由（其他页面正常跑）
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.Run();
