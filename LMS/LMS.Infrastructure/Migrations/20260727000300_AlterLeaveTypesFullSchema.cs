using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlterLeaveTypesFullSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename max_days → max_days_per_year
            migrationBuilder.RenameColumn(
                name: "max_days",
                table: "leave_types",
                newName: "max_days_per_year");

            // Shrink name column from varchar(256) → varchar(100)
            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "leave_types",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            // Add default 0 (Annual) to accrual_type
            migrationBuilder.AlterColumn<int>(
                name: "accrual_type",
                table: "leave_types",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            // Add requires_document (boolean, NOT NULL, default false)
            migrationBuilder.AddColumn<bool>(
                name: "requires_document",
                table: "leave_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "requires_document",
                table: "leave_types");

            migrationBuilder.AlterColumn<int>(
                name: "accrual_type",
                table: "leave_types",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "leave_types",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.RenameColumn(
                name: "max_days_per_year",
                table: "leave_types",
                newName: "max_days");
        }
    }
}
