using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningHistoryDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "duration_seconds",
                table: "QuizSubmission",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FlashcardReviewAttempt",
                columns: table => new
                {
                    attempt_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    u_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    card_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quality = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    time_spent_seconds = table.Column<int>(type: "int", nullable: true),
                    previous_ease_factor = table.Column<float>(type: "real", nullable: false),
                    result_ease_factor = table.Column<float>(type: "real", nullable: false),
                    previous_interval = table.Column<int>(type: "int", nullable: false),
                    result_interval = table.Column<int>(type: "int", nullable: false),
                    previous_repetitions = table.Column<int>(type: "int", nullable: false),
                    result_repetitions = table.Column<int>(type: "int", nullable: false),
                    previous_next_review_date = table.Column<DateTime>(type: "datetime", nullable: false),
                    result_next_review_date = table.Column<DateTime>(type: "datetime", nullable: false),
                    xp_earned = table.Column<int>(type: "int", nullable: false),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashcardReviewAttempt", x => x.attempt_id);
                    table.ForeignKey(
                        name: "FK_FlashcardReviewAttempt_Flashcard_card_id",
                        column: x => x.card_id,
                        principalTable: "Flashcard",
                        principalColumn: "card_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlashcardReviewAttempt_Users_u_id",
                        column: x => x.u_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardReviewAttempt_card_id",
                table: "FlashcardReviewAttempt",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardReviewAttempt_u_id_card_id_create_at",
                table: "FlashcardReviewAttempt",
                columns: new[] { "u_id", "card_id", "create_at" });

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardReviewAttempt_u_id_create_at",
                table: "FlashcardReviewAttempt",
                columns: new[] { "u_id", "create_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlashcardReviewAttempt");

            migrationBuilder.DropColumn(
                name: "duration_seconds",
                table: "QuizSubmission");
        }
    }
}
