using System;
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

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 20, 7, 40, 10, 448, DateTimeKind.Utc).AddTicks(2634));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 20, 7, 40, 10, 448, DateTimeKind.Utc).AddTicks(2641));
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

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 18, 8, 49, 35, 39, DateTimeKind.Utc).AddTicks(514));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 18, 8, 49, 35, 39, DateTimeKind.Utc).AddTicks(519));
        }
    }
}
