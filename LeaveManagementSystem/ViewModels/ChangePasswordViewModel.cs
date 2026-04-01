using System.ComponentModel.DataAnnotations;

namespace LeaveManagementSystem.ViewModels
{
    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Required]
        [MinLength(6)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Confirm password 不一致")]
        public string ConfirmPassword { get; set; }
    }
}
