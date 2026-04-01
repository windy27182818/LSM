using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using LeaveManagementSystem.Models;

namespace LeaveManagementSystem.ViewModels
{
    public class LeaveRequestCreateViewModel : LeaveRequest
    {
        [Display(Name = "Supporting Document (PDF, PNG, JPG)")]
        public IFormFile? FileAttachment { get; set; }
    }
}
