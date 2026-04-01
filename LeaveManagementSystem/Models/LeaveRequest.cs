using System;
using System.ComponentModel.DataAnnotations;

namespace LeaveManagementSystem.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public int LeaveTypeId { get; set; }
        public LeaveType? LeaveType { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public double TotalDays { get; set; }

        // 用户提交申请时必须填写
        [Required(ErrorMessage = "Reason is required.")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
        public string? Reason { get; set; }

        public string? Status { get; set; }

        public int? ApproverId { get; set; }
        public Employee? Approver { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedDate { get; set; }

        // 备注字段：是否“强制必填”要在 Controller 里根据状态(Approve/Reject)判断
        [StringLength(500, ErrorMessage = "Approver Remark cannot exceed 500 characters.")]
        public string? ApproverRemark { get; set; }

        public string? AttachmentPath { get; set; }
        public bool IsSpecialRequest { get; set; }
        public string? AttachmentBase64 { get; set; }
        public double? AnnualDeductedFromEntitlement { get; set; }
        public double? AnnualDeductedFromBalance { get; set; }

        public double AnnualCarryForwardUsed { get; set; } = 0;

    }
}
