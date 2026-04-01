using System;

namespace LeaveManagementSystem.ViewModels
{
    public class LeaveRequestListVM
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = "";
        public string LeaveTypeName { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double TotalDays { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        // ✅ 新增：给 Index.cshtml 用
        public bool IsSpecialRequest { get; set; }
        public string? AttachmentPath { get; set; }
    }
}
