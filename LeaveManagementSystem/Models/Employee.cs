using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeaveManagementSystem.Models
{
    public class Employee
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string Password { get; set; } = default!;

        public string? Department { get; set; }
        public string? Role { get; set; }

        public string EmploymentType { get; set; } = "Permanent";

        [DataType(DataType.Date)]
        public DateTime? JoinDate { get; set; }

        public double AnnualLeaveEntitlement { get; set; } = 12.0;
        public double AnnualLeaveUsed { get; set; } = 0.0;
        public double AnnualLeaveBalance { get; set; } = 12.0;

        public int SickLeaveEntitlement { get; set; } = 14;
        public int SickLeaveUsed { get; set; } = 0;
        public int SickLeaveBalance { get; set; } = 14;

        public double HospitalisationLeaveEntitlement { get; set; } = 0.0;
        public double HospitalisationLeaveUsed { get; set; } = 0.0;
        public double HospitalisationLeaveBalance { get; set; } = 0.0;

        public double MaternityLeaveEntitlement { get; set; } = 0.0;
        public double MaternityLeaveUsed { get; set; } = 0.0;
        public double MaternityLeaveBalance { get; set; } = 0.0;
        public double PaternityLeaveEntitlement { get; set; } = 0.0;
        public double PaternityLeaveUsed { get; set; } = 0.0;
        public double PaternityLeaveBalance { get; set; } = 0.0;

        public double BereavementLeaveEntitlement { get; set; } = 0.0;
        public double BereavementLeaveUsed { get; set; } = 0.0;
        public double BereavementLeaveBalance { get; set; } = 0.0;

        public double UnpaidLeaveEntitlement { get; set; } = 0.0;
        public double UnpaidLeaveUsed { get; set; } = 0.0;
        public double UnpaidLeaveBalance { get; set; } = 0.0;

        public bool IsAdmin { get; set; } = false;
        public bool IsManager { get; set; } = false;

        public int? ManagerId { get; set; }
        public Employee? Manager { get; set; }

        public List<LeaveRequest>? LeaveRequests { get; set; }

        public string? ProfilePhotoPath { get; set; }
        public List<LeaveRequest> ApprovedLeaveRequests { get; set; } = new();

        [NotMapped]
        public bool IsEligible { get; set; }

        [NotMapped]
        public DateTime? NextEligibleDate { get; set; }

        public string? APN { get; set; }
        public string? DeviceType { get; set; }

        // LAST YEAR carry forward (cap 9)
        public double AnnualCarryForward { get; set; } = 0.0;

        public bool IsBoss { get; set; }


    }
}
