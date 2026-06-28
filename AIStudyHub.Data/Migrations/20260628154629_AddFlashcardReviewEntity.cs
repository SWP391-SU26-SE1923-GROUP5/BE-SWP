using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlashcardReviewEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlashcardReviews",
                columns: table => new
                {
                    review_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    u_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    card_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ease_factor = table.Column<float>(type: "real", nullable: false),
                    interval_days = table.Column<int>(type: "int", nullable: false),
                    repetitions = table.Column<int>(type: "int", nullable: false),
                    next_review_date = table.Column<DateTime>(type: "datetime", nullable: false),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashcardReviews", x => x.review_id);
                    table.ForeignKey(
                        name: "FK_FlashcardReviews_Flashcard_card_id",
                        column: x => x.card_id,
                        principalTable: "Flashcard",
                        principalColumn: "card_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlashcardReviews_Users_u_id",
                        column: x => x.u_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 28, 15, 46, 28, 607, DateTimeKind.Utc).AddTicks(4232));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                columns: new[] { "create_at", "Price" },
                values: new object[] { new DateTime(2026, 6, 28, 15, 46, 28, 607, DateTimeKind.Utc).AddTicks(4242), 199000m });

            migrationBuilder.InsertData(
                table: "TierMembership",
                columns: new[] { "tier_id", "ai_tokens", "create_at", "Price", "storage_limit_mb", "tier_name", "update_at" },
                values: new object[] { new Guid("33333333-3333-3333-3333-333333333333"), 50000, new DateTime(2026, 6, 28, 15, 46, 28, 607, DateTimeKind.Utc).AddTicks(4238), 499000m, 5120, "Pro", null });

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardReviews_card_id",
                table: "FlashcardReviews",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardReviews_u_id_card_id",
                table: "FlashcardReviews",
                columns: new[] { "u_id", "card_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardReviews_u_id_next_review_date",
                table: "FlashcardReviews",
                columns: new[] { "u_id", "next_review_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlashcardReviews");

            migrationBuilder.DeleteData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 28, 15, 40, 38, 365, DateTimeKind.Utc).AddTicks(8364));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                columns: new[] { "create_at", "Price" },
                values: new object[] { new DateTime(2026, 6, 28, 15, 40, 38, 365, DateTimeKind.Utc).AddTicks(8367), 0m });
        }
    }
}
