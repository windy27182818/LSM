using System;

namespace LeaveManagementSystem.ViewModels
{
    public class LeaveAdminViewModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Department { get; set; }
        public string? EmploymentType { get; set; }
        public DateTime? JoinDate { get; set; }

        // Annual Leave
        public double AnnualLeaveEntitlement { get; set; }
        public double AnnualLeaveUsed { get; set; }
        public double AnnualLeaveBalance { get; set; }
        public double AnnualCarryForward { get; set; }


        // Sick Leave
        public int SickLeaveEntitlement { get; set; }
        public int SickLeaveUsed { get; set; }
        public int SickLeaveBalance { get; set; }

        // Hospitalisation Leave
        public double HospitalisationLeaveEntitlement { get; set; }
        public double HospitalisationLeaveUsed { get; set; }
        public double HospitalisationLeaveBalance { get; set; }

        // Maternity Leave
        public double MaternityLeaveEntitlement { get; set; }
        public double MaternityLeaveUsed { get; set; }
        public double MaternityLeaveBalance { get; set; }

        // Paternity Leave
        public double PaternityLeaveEntitlement { get; set; }
        public double PaternityLeaveUsed { get; set; }
        public double PaternityLeaveBalance { get; set; }

        // Bereavement Leave
        public double BereavementLeaveEntitlement { get; set; }
        public double BereavementLeaveUsed { get; set; }
        public double BereavementLeaveBalance { get; set; }

        // Unpaid Leave
        public double UnpaidLeaveEntitlement { get; set; }
        public double UnpaidLeaveUsed { get; set; }
        public double UnpaidLeaveBalance { get; set; }

        public bool IsAdmin { get; set; }
    }
}
