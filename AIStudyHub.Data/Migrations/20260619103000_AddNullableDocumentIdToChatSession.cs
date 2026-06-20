using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNullableDocumentIdToChatSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSession_Document_doc_id",
                table: "ChatSession");

            migrationBuilder.DropIndex(
                name: "IX_ChatSession_doc_id",
                table: "ChatSession");

            migrationBuilder.AlterColumn<Guid>(
                name: "doc_id",
                table: "ChatSession",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSession_Document_doc_id",
                table: "ChatSession");

            migrationBuilder.DropIndex(
                name: "IX_ChatSession_doc_id",
                table: "ChatSession");

            migrationBuilder.AlterColumn<Guid>(
                name: "doc_id",
                table: "ChatSession",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

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
                onDelete: ReferentialAction.Cascade);
        }
    }
}
