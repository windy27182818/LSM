using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreLeaveTypesToEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BereavementLeaveBalance",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "BereavementLeaveEntitlement",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "BereavementLeaveUsed",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "HospitalisationLeaveBalance",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "HospitalisationLeaveEntitlement",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "HospitalisationLeaveUsed",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MaternityLeaveBalance",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MaternityLeaveEntitlement",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MaternityLeaveUsed",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "UnpaidLeaveBalance",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "UnpaidLeaveEntitlement",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "UnpaidLeaveUsed",
                table: "Employees",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BereavementLeaveBalance",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "BereavementLeaveEntitlement",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "BereavementLeaveUsed",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "HospitalisationLeaveBalance",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "HospitalisationLeaveEntitlement",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "HospitalisationLeaveUsed",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "MaternityLeaveBalance",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "MaternityLeaveEntitlement",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "MaternityLeaveUsed",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UnpaidLeaveBalance",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UnpaidLeaveEntitlement",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UnpaidLeaveUsed",
                table: "Employees");
        }
    }
}
