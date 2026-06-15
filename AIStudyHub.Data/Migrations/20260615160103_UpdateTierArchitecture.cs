using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTierArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TierUser");

            migrationBuilder.AddColumn<DateTime>(
                name: "tier_expire_at",
                table: "Users",
                type: "datetime",
                nullable: true);

            migrationBuilder.InsertData(
                table: "TierMembership",
                columns: new[] { "tier_id", "ai_tokens", "create_at", "storage_limit_mb", "tier_name", "update_at" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), 10000, new DateTime(2026, 6, 15, 16, 1, 0, 117, DateTimeKind.Utc).AddTicks(3402), 1024, "Free", null },
                    { new Guid("55555555-5555-5555-5555-555555555555"), 30000, new DateTime(2026, 6, 15, 16, 1, 0, 117, DateTimeKind.Utc).AddTicks(3406), 3072, "Premium", null }
                });

            migrationBuilder.AddColumn<Guid>(
                name: "tier_id",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.CreateIndex(
                name: "IX_Users_tier_id",
                table: "Users",
                column: "tier_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_TierMembership_tier_id",
                table: "Users",
                column: "tier_id",
                principalTable: "TierMembership",
                principalColumn: "tier_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_TierMembership_tier_id",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_tier_id",
                table: "Users");

            migrationBuilder.DeleteData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"));

            migrationBuilder.DropColumn(
                name: "tier_expire_at",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "tier_id",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "TierUser",
                columns: table => new
                {
                    tier_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tier_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    u_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TierUser", x => x.tier_user_id);
                    table.ForeignKey(
                        name: "FK_TierUser_TierMembership_tier_id",
                        column: x => x.tier_id,
                        principalTable: "TierMembership",
                        principalColumn: "tier_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TierUser_Users_u_id",
                        column: x => x.u_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TierUser_tier_id",
                table: "TierUser",
                column: "tier_id");

            migrationBuilder.CreateIndex(
                name: "IX_TierUser_u_id",
                table: "TierUser",
                column: "u_id");
        }
    }
}
