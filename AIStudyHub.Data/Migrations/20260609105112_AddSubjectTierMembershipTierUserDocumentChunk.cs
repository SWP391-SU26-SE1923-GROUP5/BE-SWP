using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectTierMembershipTierUserDocumentChunk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Payments",
                newName: "u_id");

            migrationBuilder.RenameIndex(
                name: "IX_Payments_UserId",
                table: "Payments",
                newName: "IX_Payments_u_id");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Documents",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Documents",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Documents",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Documents",
                newName: "u_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Documents",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "FileUrl",
                table: "Documents",
                newName: "file_link");

            migrationBuilder.RenameColumn(
                name: "FileSizeBytes",
                table: "Documents",
                newName: "file_size_bytes");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Documents",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "ContentType",
                table: "Documents",
                newName: "file_type");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_UserId",
                table: "Documents",
                newName: "IX_Documents_u_id");

            migrationBuilder.AddColumn<Guid>(
                name: "tier_id",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "Documents",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "Documents",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<Guid>(
                name: "subject_id",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    doc_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    chunk_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    embedding_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentChunks_Documents_doc_id",
                        column: x => x.doc_id,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    subject_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    subject_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TierMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tier_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    storage_limit_mb = table.Column<int>(type: "int", nullable: false),
                    ai_tokens = table.Column<int>(type: "int", nullable: false),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TierMemberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TierUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    u_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tier_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TierUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TierUsers_TierMemberships_tier_id",
                        column: x => x.tier_id,
                        principalTable: "TierMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TierUsers_Users_u_id",
                        column: x => x.u_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_tier_id",
                table: "Payments",
                column: "tier_id");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_subject_id",
                table: "Documents",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_doc_id",
                table: "DocumentChunks",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_subject_code",
                table: "Subjects",
                column: "subject_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TierUsers_tier_id",
                table: "TierUsers",
                column: "tier_id");

            migrationBuilder.CreateIndex(
                name: "IX_TierUsers_u_id_tier_id",
                table: "TierUsers",
                columns: new[] { "u_id", "tier_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Subjects_subject_id",
                table: "Documents",
                column: "subject_id",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_u_id",
                table: "Documents",
                column: "u_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_TierMemberships_tier_id",
                table: "Payments",
                column: "tier_id",
                principalTable: "TierMemberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_u_id",
                table: "Payments",
                column: "u_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Subjects_subject_id",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_u_id",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_TierMemberships_tier_id",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_u_id",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "DocumentChunks");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropTable(
                name: "TierUsers");

            migrationBuilder.DropTable(
                name: "TierMemberships");

            migrationBuilder.DropIndex(
                name: "IX_Payments_tier_id",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Documents_subject_id",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "tier_id",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "subject_id",
                table: "Documents");

            migrationBuilder.RenameColumn(
                name: "u_id",
                table: "Payments",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Payments_u_id",
                table: "Payments",
                newName: "IX_Payments_UserId");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "Documents",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "Documents",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "Documents",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "Documents",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "u_id",
                table: "Documents",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "file_type",
                table: "Documents",
                newName: "ContentType");

            migrationBuilder.RenameColumn(
                name: "file_size_bytes",
                table: "Documents",
                newName: "FileSizeBytes");

            migrationBuilder.RenameColumn(
                name: "file_link",
                table: "Documents",
                newName: "FileUrl");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "Documents",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_u_id",
                table: "Documents",
                newName: "IX_Documents_UserId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Documents",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Documents",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
