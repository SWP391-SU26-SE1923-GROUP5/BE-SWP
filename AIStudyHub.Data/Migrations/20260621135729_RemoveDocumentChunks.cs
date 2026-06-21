using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDocumentChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Document_Chunk");

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 21, 13, 57, 27, 762, DateTimeKind.Utc).AddTicks(8645));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 21, 13, 57, 27, 762, DateTimeKind.Utc).AddTicks(8647));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Document_Chunk",
                columns: table => new
                {
                    document_chunk_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    doc_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    chunk_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    order_index = table.Column<int>(type: "int", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    vector_id = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Document_Chunk", x => x.document_chunk_id);
                    table.ForeignKey(
                        name: "FK_Document_Chunk_Document_doc_id",
                        column: x => x.doc_id,
                        principalTable: "Document",
                        principalColumn: "doc_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 21, 9, 2, 22, 734, DateTimeKind.Utc).AddTicks(2773));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 21, 9, 2, 22, 734, DateTimeKind.Utc).AddTicks(2775));

            migrationBuilder.CreateIndex(
                name: "IX_Document_Chunk_doc_id",
                table: "Document_Chunk",
                column: "doc_id");
        }
    }
}
