using System;
using System.ComponentModel.DataAnnotations;

namespace LeaveManagementSystem.ViewModels
{
    public class EmployeeLeaveWalletEditVM
    {
        public int Id { get; set; }

        // Employee Info (read-only display)
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Department { get; set; }
        public string? EmploymentType { get; set; }
        public DateTime? JoinDate { get; set; }

        // Role
        public bool IsAdmin { get; set; }
        public string? Role { get; set; }

        // ===== Annual =====
        [Range(0, 365)]
        public double AnnualLeaveEntitlement { get; set; }
        [Range(0, 365)]
        public double AnnualLeaveUsed { get; set; }
        public double AnnualLeaveBalance { get; set; }

        // ===== Sick =====
        [Range(0, 365)]
        public int SickLeaveEntitlement { get; set; }
        [Range(0, 365)]
        public int SickLeaveUsed { get; set; }
        public int SickLeaveBalance { get; set; }

        // ===== Hospitalisation =====
        [Range(0, 365)]
        public double HospitalisationLeaveEntitlement { get; set; }
        [Range(0, 365)]
        public double HospitalisationLeaveUsed { get; set; }
        public double HospitalisationLeaveBalance { get; set; }

        // ===== Maternity =====
        [Range(0, 365)]
        public double MaternityLeaveEntitlement { get; set; }
        [Range(0, 365)]
        public double MaternityLeaveUsed { get; set; }
        public double MaternityLeaveBalance { get; set; }

        // ===== Paternity =====
        [Range(0, 365)]
        public double PaternityLeaveEntitlement { get; set; }
        [Range(0, 365)]
        public double PaternityLeaveUsed { get; set; }
        public double PaternityLeaveBalance { get; set; }

        // ===== Bereavement =====
        [Range(0, 365)]
        public double BereavementLeaveEntitlement { get; set; }
        [Range(0, 365)]
        public double BereavementLeaveUsed { get; set; }
        public double BereavementLeaveBalance { get; set; }

        // ===== Unpaid =====
        [Range(0, 365)]
        public double UnpaidLeaveEntitlement { get; set; }
        [Range(0, 365)]
        public double UnpaidLeaveUsed { get; set; }
        public double UnpaidLeaveBalance { get; set; }
    }
}
