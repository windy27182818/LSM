using System;
using System.Collections.Generic;

namespace LeaveManagementSystem.ViewModels
{
    public class ClaimDashboardViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public int TotalClaims { get; set; }
        public int ApprovedCount { get; set; }
        public int PendingCount { get; set; }
        public int RejectedCount { get; set; }

        public List<ClaimTypeSummary> ClaimsByType { get; set; } = new();
        public List<ClaimHistoryRow> ClaimHistory { get; set; } = new();

        public int? SelectedEmployeeId { get; set; }
    }

    public class ClaimTypeSummary
    {
        public string ClaimTypeName { get; set; } = "";
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class ClaimHistoryRow
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = "";
        public string ClaimTypeName { get; set; } = "";
        public decimal Amount { get; set; }
        public string Status { get; set; } = "";
        public DateTime RequestDate { get; set; }
        public bool EmployeeIsEligible { get; set; }
        public DateTime? EmployeeNextEligibleDate { get; set; }
        public string? Receipt { get; set; }

    }
}
