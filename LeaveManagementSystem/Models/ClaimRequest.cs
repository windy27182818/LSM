using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeaveManagementSystem.Models
{
    public class ClaimRequest
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Employee")]
        public int EmployeeId { get; set; }

        public Employee? Employee { get; set; }

        [Required]
        public DateTime RequestDate { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public string Description { get; set; }

        public string? Receipt { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime? ApprovedDate { get; set; }

        public string? ApproverRemarks { get; set; }

        [ForeignKey("ClaimType")]
        public int ClaimTypeId { get; set; }

        public ClaimType? ClaimType { get; set; }
    }
}