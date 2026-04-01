using Microsoft.AspNetCore.Mvc;

namespace LeaveManagementSystem.Helpers
{
    public static class DepartmentAccess
    {
        public static bool IsSaleOrAcc(string? dept)
        {
            if (string.IsNullOrWhiteSpace(dept)) return false;

            var d = dept.Trim().ToLowerInvariant();

            // Sale
            if (d == "sale" || d == "sales" || d == "sale department") return true;

            // Acc (accounting family)
            if (d == "acc" || d == "account" || d == "accounts" || d == "accounting") return true;

            return false;
        }
    }
}
