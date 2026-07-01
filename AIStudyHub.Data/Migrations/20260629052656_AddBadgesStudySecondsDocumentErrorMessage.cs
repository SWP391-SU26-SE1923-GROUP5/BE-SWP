using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBadgesStudySecondsDocumentErrorMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "total_study_seconds",
                table: "UserStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "Document",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Badge",
                columns: table => new
                {
                    badge_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    target_value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    icon_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    xp_reward = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Badge", x => x.badge_id);
                });

            migrationBuilder.CreateTable(
                name: "UserBadge",
                columns: table => new
                {
                    user_badge_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    u_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    badge_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    earned_date = table.Column<DateTime>(type: "datetime", nullable: false),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBadge", x => x.user_badge_id);
                    table.ForeignKey(
                        name: "FK_UserBadge_Badge_badge_id",
                        column: x => x.badge_id,
                        principalTable: "Badge",
                        principalColumn: "badge_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBadge_Users_u_id",
                        column: x => x.u_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Badge",
                columns: new[] { "badge_id", "category", "code", "create_at", "description", "icon_url", "target_value", "title", "update_at", "xp_reward" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-0001-0000-0000-000000000001"), "Streak", "STREAK_7D", new DateTime(2026, 6, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Maintain a 7-day study streak.", "/badges/streak-7d.svg", 7m, "7-Day Streak", null, 100 },
                    { new Guid("aaaaaaaa-0002-0000-0000-000000000002"), "Volume", "CARDS_500", new DateTime(2026, 6, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Review 500 flashcards.", "/badges/memory-master.svg", 500m, "Memory Master", null, 150 },
                    { new Guid("aaaaaaaa-0003-0000-0000-000000000003"), "Mastery", "MASTERY_MATH", new DateTime(2026, 6, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Reach 85% or higher in Mathematics.", "/badges/math-prodigy.svg", 85m, "Math Prodigy", null, 120 },
                    { new Guid("aaaaaaaa-0004-0000-0000-000000000004"), "Accuracy", "SHARPSHOOTER", new DateTime(2026, 6, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Score 100% on a quiz with at least 10 questions on the first attempt.", "/badges/sharpshooter.svg", 100m, "Sharpshooter", null, 200 },
                    { new Guid("aaaaaaaa-0005-0000-0000-000000000005"), "Content", "BOOKWORM", new DateTime(2026, 6, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Successfully process 7 documents.", "/badges/bookworm.svg", 7m, "Bookworm", null, 80 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Badge_code",
                table: "Badge",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBadge_badge_id",
                table: "UserBadge",
                column: "badge_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadge_u_id_badge_id",
                table: "UserBadge",
                columns: new[] { "u_id", "badge_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserBadge");

            migrationBuilder.DropTable(
                name: "Badge");

            migrationBuilder.DropColumn(
                name: "total_study_seconds",
                table: "UserStats");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "Document");
        }
    }
}
