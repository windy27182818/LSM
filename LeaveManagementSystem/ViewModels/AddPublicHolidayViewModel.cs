using System;
using System.ComponentModel.DataAnnotations;

namespace LeaveManagementSystem.ViewModels
{
    public class AddPublicHolidayViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; }

        public string? Type { get; set; }

        public string? Location { get; set; }
    }
}
