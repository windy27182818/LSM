using System.ComponentModel.DataAnnotations;

namespace LeaveManagementSystem.Models
{
    // 这将对应数据库中的 ClaimTypes 表
    public class ClaimType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } // 例如: "Medical", "Travel"

        public string? Description { get; set; } // 例如: "医疗报销，每人每年限额..."

        // 你甚至可以添加限额字段，模仿 LeaveType 的设计
        // public decimal MaxAmountPerYear { get; set; } 
    }
}