using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandDepartmentsFullSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tighten name column from varchar(256) to varchar(100) per FR-21
            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "departments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            // Add optional description column
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "departments",
                type: "text",
                nullable: true);

            // Add is_active for soft delete (default true — existing rows become active)
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "departments",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Index to accelerate soft-delete filter queries
            migrationBuilder.CreateIndex(
                name: "ix_departments_is_active",
                table: "departments",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_departments_is_active",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "description",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "departments");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "departments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
