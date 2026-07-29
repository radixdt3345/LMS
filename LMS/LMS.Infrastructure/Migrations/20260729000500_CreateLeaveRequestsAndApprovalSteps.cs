using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateLeaveRequestsAndApprovalSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leave_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    computed_days = table.Column<decimal>(type: "numeric(5,1)", nullable: false, defaultValue: 0m),
                    status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    is_retroactive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    document_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_leave_requests_users_employee_id",
                        column: x => x.employee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leave_requests_leave_types_leave_type_id",
                        column: x => x.leave_type_id,
                        principalTable: "leave_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_employee_id",
                table: "leave_requests",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_status",
                table: "leave_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_start_date",
                table: "leave_requests",
                column: "start_date");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_employee_id_status",
                table: "leave_requests",
                columns: new[] { "employee_id", "status" });

            migrationBuilder.CreateTable(
                name: "approval_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    leave_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_number = table.Column<short>(type: "smallint", nullable: false),
                    approver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    acted_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_approval_steps_leave_requests_leave_request_id",
                        column: x => x.leave_request_id,
                        principalTable: "leave_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_approval_steps_users_approver_id",
                        column: x => x.approver_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_steps_leave_request_id",
                table: "approval_steps",
                column: "leave_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_steps_approver_id_status",
                table: "approval_steps",
                columns: new[] { "approver_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "approval_steps");
            migrationBuilder.DropTable(name: "leave_requests");
        }
    }
}
