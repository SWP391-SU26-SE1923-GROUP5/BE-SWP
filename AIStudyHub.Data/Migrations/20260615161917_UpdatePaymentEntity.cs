using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePaymentEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "tier_id",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<decimal>(
                name: "amount",
                table: "Payment",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "transaction_id",
                table: "Payment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 15, 16, 19, 14, 796, DateTimeKind.Utc).AddTicks(3932));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 15, 16, 19, 14, 796, DateTimeKind.Utc).AddTicks(3935));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "amount",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "transaction_id",
                table: "Payment");

            migrationBuilder.AlterColumn<Guid>(
                name: "tier_id",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 15, 16, 1, 0, 117, DateTimeKind.Utc).AddTicks(3402));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 15, 16, 1, 0, 117, DateTimeKind.Utc).AddTicks(3406));
        }
    }
}
