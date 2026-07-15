using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProcessingVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_reindex_error",
                table: "Document",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "processing_version",
                table: "Document",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "reindex_attempt_count",
                table: "Document",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "reindex_claim_id",
                table: "Document",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reindex_claimed_at",
                table: "Document",
                type: "datetime",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Document_ReindexEligibility",
                table: "Document",
                columns: new[] { "processing_version", "reindex_claimed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Document_ReindexEligibility",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "last_reindex_error",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "processing_version",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "reindex_attempt_count",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "reindex_claim_id",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "reindex_claimed_at",
                table: "Document");
        }
    }
}
