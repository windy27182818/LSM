using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace LeaveManagementSystem.ViewModels
{
    public class DashboardViewModel
    {
        public int ThisMonthTotal { get; set; }
        public int ThisMonthApproved { get; set; }
        public int ThisMonthPending { get; set; }
        public int ThisMonthRejected { get; set; }
        public int TotalPending { get; set; }
        public List<LeaveTypeSummary>? ByLeaveType { get; set; }

        public class LeaveTypeSummary
        {
            public string? LeaveTypeName { get; set; }
            public int Count { get; set; }
        }
        public int? EmployeeFilterId { get; set; }
        public int? FilterYear { get; set; }
        public int? FilterMonth { get; set; }

        public IEnumerable<SelectListItem>? EmployeeList { get; set; }
        public IEnumerable<int>? AvailableYears { get; set; }

        public List<LeaveHistoryItem>? History { get; set; }

        public class LeaveHistoryItem
        {
            public int Id { get; set; }
            public string? EmployeeName { get; set; }
            public string? LeaveTypeName { get; set; }
            public System.DateTime StartDate { get; set; }
            public System.DateTime EndDate { get; set; }
            public double TotalDays { get; set; }
            public string? Status { get; set; }
            public string? AttachmentPath { get; set; }
        }
    }
}
