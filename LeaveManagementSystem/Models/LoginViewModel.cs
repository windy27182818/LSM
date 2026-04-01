using System.ComponentModel.DataAnnotations;

namespace LeaveManagementSystem.Models
{
    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Employee Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me (On this device)")]
        public bool RememberMe { get; set; }
    }
}
