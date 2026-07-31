using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlashcardDeckEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlashcardDeck",
                columns: table => new
                {
                    deck_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    doc_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashcardDeck", x => x.deck_id);
                    table.ForeignKey(
                        name: "FK_FlashcardDeck_Document_doc_id",
                        column: x => x.doc_id,
                        principalTable: "Document",
                        principalColumn: "doc_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardDeck_doc_id",
                table: "FlashcardDeck",
                column: "doc_id");

            migrationBuilder.Sql(@"
                INSERT INTO [FlashcardDeck] ([deck_id], [doc_id], [name], [create_at], [update_at])
                SELECT NEWID(), [doc_id], 'Default Deck', GETUTCDATE(), NULL
                FROM (SELECT DISTINCT [doc_id] FROM [Flashcard]) AS docs");

            migrationBuilder.DropForeignKey(
                name: "FK_Flashcard_Document_doc_id",
                table: "Flashcard");

            migrationBuilder.RenameColumn(
                name: "doc_id",
                table: "Flashcard",
                newName: "deck_id");

            migrationBuilder.RenameIndex(
                name: "IX_Flashcard_doc_id",
                table: "Flashcard",
                newName: "IX_Flashcard_deck_id");

            migrationBuilder.Sql(@"
                UPDATE [Flashcard]
                SET [deck_id] = fd.[deck_id]
                FROM [Flashcard] f
                INNER JOIN [FlashcardDeck] fd ON fd.[doc_id] = f.[deck_id]");

            migrationBuilder.AddForeignKey(
                name: "FK_Flashcard_FlashcardDeck_deck_id",
                table: "Flashcard",
                column: "deck_id",
                principalTable: "FlashcardDeck",
                principalColumn: "deck_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Flashcard_FlashcardDeck_deck_id",
                table: "Flashcard");

            migrationBuilder.Sql("UPDATE [Flashcard] SET [deck_id] = (SELECT [doc_id] FROM [FlashcardDeck] WHERE [deck_id] = [Flashcard].[deck_id])");

            migrationBuilder.RenameColumn(
                name: "deck_id",
                table: "Flashcard",
                newName: "doc_id");

            migrationBuilder.RenameIndex(
                name: "IX_Flashcard_deck_id",
                table: "Flashcard",
                newName: "IX_Flashcard_doc_id");

            migrationBuilder.DropTable(
                name: "FlashcardDeck");

            migrationBuilder.AddForeignKey(
                name: "FK_Flashcard_Document_doc_id",
                table: "Flashcard",
                column: "doc_id",
                principalTable: "Document",
                principalColumn: "doc_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
