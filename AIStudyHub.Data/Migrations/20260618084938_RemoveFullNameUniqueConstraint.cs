using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFullNameUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_full_name",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 18, 8, 49, 35, 39, DateTimeKind.Utc).AddTicks(514));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 18, 8, 49, 35, 39, DateTimeKind.Utc).AddTicks(519));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 17, 5, 56, 15, 789, DateTimeKind.Utc).AddTicks(6066));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 17, 5, 56, 15, 789, DateTimeKind.Utc).AddTicks(6070));

            migrationBuilder.CreateIndex(
                name: "IX_Users_full_name",
                table: "Users",
                column: "full_name",
                unique: true);
        }
    }
}
