using System;
using System.ComponentModel.DataAnnotations;

namespace LeaveManagementSystem.ViewModels
{
    public class ClaimRequestFormViewModel
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        public string? EmployeeName { get; set; }

        [Required]
        [Display(Name = "Amount ($)")]
        [Range(0.01, 100000, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please select a claim type")]
        [Display(Name = "Claim Type")]
        public int ClaimTypeId { get; set; }

        // 這裡存放圖片的 Base64 字串 (從資料庫讀取用於顯示，或隱藏傳遞)
        public string? Receipt { get; set; }

        // 狀態改為 string 以匹配您的資料庫定義
        public string Status { get; set; } = "Pending";

        public bool IsAdmin { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ApprovedDate { get; set; }

        public string? ApproverRemarks { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int CurrentUserId { get; set; }

    }
}