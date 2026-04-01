using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /*
                migrationBuilder.CreateTable(
                name: "ClaimTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimTypes", x => x.Id);
                });
            
            migrationBuilder.CreateTable(
                name: "ClaimRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Receipt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApproverRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimRequests_ClaimTypes_ClaimTypeId",
                        column: x => x.ClaimTypeId,
                        principalTable: "ClaimTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClaimRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRequests_ClaimTypeId",
                table: "ClaimRequests",
                column: "ClaimTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRequests_EmployeeId",
                table: "ClaimRequests",
                column: "EmployeeId");
        */
        }

            /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimRequests");

            migrationBuilder.DropTable(
                name: "ClaimTypes");
        }
    }
}
