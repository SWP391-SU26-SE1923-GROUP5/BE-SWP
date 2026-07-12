using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSessionDocumentsManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatSessionDocument",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    session_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    doc_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessionDocument", x => x.id);
                    table.ForeignKey(
                        name: "FK_ChatSessionDocument_ChatSession_session_id",
                        column: x => x.session_id,
                        principalTable: "ChatSession",
                        principalColumn: "session_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatSessionDocument_Document_doc_id",
                        column: x => x.doc_id,
                        principalTable: "Document",
                        principalColumn: "doc_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessionDocument_doc_id",
                table: "ChatSessionDocument",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessionDocument_session_id_doc_id",
                table: "ChatSessionDocument",
                columns: new[] { "session_id", "doc_id" },
                unique: true);

            // Migrate existing ChatSession.doc_id values into the join table
            migrationBuilder.Sql(@"
                INSERT INTO [ChatSessionDocument] ([id], [session_id], [doc_id], [create_at], [update_at])
                SELECT NEWID(), [session_id], [doc_id], GETUTCDATE(), GETUTCDATE()
                FROM [ChatSession]
                WHERE [doc_id] IS NOT NULL;
            ");

            // Drop the old FK and index on ChatSession, then drop the column
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSession_Document_doc_id",
                table: "ChatSession");

            migrationBuilder.DropIndex(
                name: "IX_ChatSession_doc_id",
                table: "ChatSession");

            migrationBuilder.DropColumn(
                name: "doc_id",
                table: "ChatSession");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore doc_id column from join table before dropping it
            migrationBuilder.Sql(@"
                ALTER TABLE [ChatSession] ADD [doc_id] uniqueidentifier NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE cs
                SET cs.[doc_id] = csd.[doc_id]
                FROM [ChatSession] cs
                INNER JOIN [ChatSessionDocument] csd ON cs.[session_id] = csd.[session_id];
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSession_doc_id",
                table: "ChatSession",
                column: "doc_id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSession_Document_doc_id",
                table: "ChatSession",
                column: "doc_id",
                principalTable: "Document",
                principalColumn: "doc_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropTable(
                name: "ChatSessionDocument");
        }
    }
}
