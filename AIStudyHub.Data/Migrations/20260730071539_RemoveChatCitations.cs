using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveChatCitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessageCitation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatMessageCitation",
                columns: table => new
                {
                    citation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    message_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    citation_index = table.Column<int>(type: "int", nullable: false),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    is_highlightable = table.Column<bool>(type: "bit", nullable: false),
                    match_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    page_number = table.Column<int>(type: "int", nullable: true),
                    reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    relevance = table.Column<double>(type: "float", nullable: false),
                    snippet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    source = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessageCitation", x => x.citation_id);
                    table.CheckConstraint("CK_ChatMessageCitation_CitationIndex_Positive", "[citation_index] > 0");
                    table.CheckConstraint("CK_ChatMessageCitation_DocumentId_NotEmpty", "[document_id] <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "FK_ChatMessageCitation_ChatMessage_message_id",
                        column: x => x.message_id,
                        principalTable: "ChatMessage",
                        principalColumn: "message_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageCitation_message_id_citation_index",
                table: "ChatMessageCitation",
                columns: new[] { "message_id", "citation_index" },
                unique: true);
        }
    }
}
