using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class CheckPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 28, 16, 4, 57, 970, DateTimeKind.Utc).AddTicks(1050));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "create_at",
                value: new DateTime(2026, 6, 28, 16, 4, 57, 970, DateTimeKind.Utc).AddTicks(1056));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 28, 16, 4, 57, 970, DateTimeKind.Utc).AddTicks(1058));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 28, 15, 51, 21, 826, DateTimeKind.Utc).AddTicks(8324));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "create_at",
                value: new DateTime(2026, 6, 28, 15, 51, 21, 826, DateTimeKind.Utc).AddTicks(8330));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 28, 15, 51, 21, 826, DateTimeKind.Utc).AddTicks(8333));
        }
    }
}
