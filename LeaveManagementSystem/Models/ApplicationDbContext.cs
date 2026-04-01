using Microsoft.EntityFrameworkCore;

namespace LeaveManagementSystem.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<PublicHoliday> PublicHolidays { get; set; }
        public DbSet<ClaimRequest> ClaimRequests { get; set; }
        public DbSet<ClaimType> ClaimTypes { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<LeaveRequest>()
                .HasOne(l => l.Employee)
                .WithMany(e => e.LeaveRequests)
                .HasForeignKey(l => l.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LeaveRequest>()
                .HasOne(l => l.Approver)
                .WithMany(e => e.ApprovedLeaveRequests)
                .HasForeignKey(l => l.ApproverId)
                .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
