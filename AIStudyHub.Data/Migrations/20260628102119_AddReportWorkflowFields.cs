using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Report_u_id",
                table: "Report");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "TierMembership",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "category",
                table: "Report",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at",
                table: "Report",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "resolved_by",
                table: "Report",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "Report",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "is_non_flaggable",
                table: "Document",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "create_at", "Price" },
                values: new object[] { new DateTime(2026, 6, 28, 10, 21, 18, 215, DateTimeKind.Utc).AddTicks(62), 0m });

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                columns: new[] { "create_at", "Price" },
                values: new object[] { new DateTime(2026, 6, 28, 10, 21, 18, 215, DateTimeKind.Utc).AddTicks(67), 0m });

            migrationBuilder.CreateIndex(
                name: "IX_Report_resolved_by",
                table: "Report",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_UserId_DocumentId_Pending",
                table: "Report",
                columns: new[] { "u_id", "doc_id" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Report_Users_resolved_by",
                table: "Report",
                column: "resolved_by",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Report_Users_resolved_by",
                table: "Report");

            migrationBuilder.DropIndex(
                name: "IX_Report_resolved_by",
                table: "Report");

            migrationBuilder.DropIndex(
                name: "IX_Reports_UserId_DocumentId_Pending",
                table: "Report");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "TierMembership");

            migrationBuilder.DropColumn(
                name: "category",
                table: "Report");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                table: "Report");

            migrationBuilder.DropColumn(
                name: "resolved_by",
                table: "Report");

            migrationBuilder.DropColumn(
                name: "status",
                table: "Report");

            migrationBuilder.DropColumn(
                name: "is_non_flaggable",
                table: "Document");

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 26, 2, 56, 3, 123, DateTimeKind.Utc).AddTicks(3539));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 26, 2, 56, 3, 123, DateTimeKind.Utc).AddTicks(3541));

            migrationBuilder.CreateIndex(
                name: "IX_Report_u_id",
                table: "Report",
                column: "u_id");
        }
    }
}
