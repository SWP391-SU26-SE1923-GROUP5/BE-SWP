using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageCitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatMessageCitation",
                columns: table => new
                {
                    citation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    message_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    citation_index = table.Column<int>(type: "int", nullable: false),
                    document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    snippet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    page_number = table.Column<int>(type: "int", nullable: true),
                    relevance = table.Column<double>(type: "float", nullable: false),
                    match_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    is_highlightable = table.Column<bool>(type: "bit", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessageCitation", x => x.citation_id);
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessageCitation");
        }
    }
}
