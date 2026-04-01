using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace LeaveManagementSystem.Services
{
    public interface IDeptScope
    {
        bool IsBossView { get; }
        string[] AllowedDepartments { get; }
    }

    public class DeptScope : IDeptScope
    {
        private readonly IHttpContextAccessor _http;

        public DeptScope(IHttpContextAccessor http)
        {
            _http = http;
        }

        public bool IsBossView =>
            _http.HttpContext?.User?.FindFirstValue("Role") == "BOSS_VIEW";

        public string[] AllowedDepartments => new[] { "SALE", "ACCOUNT" };
    }
}
