using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateCompOffTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── comp_off_requests ────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "comp_off_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worked_date = table.Column<DateOnly>(type: "date", nullable: false),
                    worked_hours = table.Column<decimal>(type: "numeric(4,1)", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comp_off_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_comp_off_requests_users_employee_id",
                        column: x => x.employee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // UNIQUE: one request per employee per worked day
            migrationBuilder.CreateIndex(
                name: "ix_comp_off_requests_employee_id_worked_date",
                table: "comp_off_requests",
                columns: new[] { "employee_id", "worked_date" },
                unique: true);

            // ── comp_off_credits ─────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "comp_off_credits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comp_off_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_days = table.Column<decimal>(type: "numeric(3,1)", nullable: false),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: false),
                    used_days = table.Column<decimal>(type: "numeric(3,1)", nullable: false, defaultValue: 0m),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comp_off_credits", x => x.id);
                    table.ForeignKey(
                        name: "fk_comp_off_credits_users_employee_id",
                        column: x => x.employee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_comp_off_credits_comp_off_requests",
                        column: x => x.comp_off_request_id,
                        principalTable: "comp_off_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_comp_off_credits_employee_id",
                table: "comp_off_credits",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_comp_off_credits_comp_off_request_id",
                table: "comp_off_credits",
                column: "comp_off_request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "comp_off_credits");
            migrationBuilder.DropTable(name: "comp_off_requests");
        }
    }
}
