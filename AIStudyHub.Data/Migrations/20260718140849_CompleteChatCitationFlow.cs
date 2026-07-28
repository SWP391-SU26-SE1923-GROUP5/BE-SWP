using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteChatCitationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_relevant",
                table: "ChatMessage",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatMessageCitation_CitationIndex_Positive",
                table: "ChatMessageCitation",
                sql: "[citation_index] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatMessageCitation_DocumentId_NotEmpty",
                table: "ChatMessageCitation",
                sql: "[document_id] <> '00000000-0000-0000-0000-000000000000'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatMessageCitation_CitationIndex_Positive",
                table: "ChatMessageCitation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatMessageCitation_DocumentId_NotEmpty",
                table: "ChatMessageCitation");

            migrationBuilder.DropColumn(
                name: "is_relevant",
                table: "ChatMessage");
        }
    }
}
