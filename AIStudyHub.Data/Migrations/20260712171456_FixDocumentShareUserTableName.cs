using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixDocumentShareUserTableName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notification_u_id",
                table: "Notification");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Notification",
                newName: "type");

            migrationBuilder.AddColumn<string>(
                name: "action_url",
                table: "Notification",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payload_json",
                table: "Notification",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "Notification",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "lapses",
                table: "Flashcard",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleStatus",
                table: "Document",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrashedAt",
                table: "Document",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TrashedBy",
                table: "Document",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentShare",
                columns: table => new
                {
                    share_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    doc_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    u_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    level = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    shared_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    shared_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentShare", x => x.share_id);
                    table.ForeignKey(
                        name: "FK_DocumentShare_Document_doc_id",
                        column: x => x.doc_id,
                        principalTable: "Document",
                        principalColumn: "doc_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentShare_Users_u_id",
                        column: x => x.u_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Recommendations",
                columns: table => new
                {
                    recommendation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    u_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<int>(type: "int", nullable: false),
                    reference_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    action_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    dismissed_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendations", x => x.recommendation_id);
                    table.ForeignKey(
                        name: "FK_Recommendations_Users_u_id",
                        column: x => x.u_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TokenLedger",
                columns: table => new
                {
                    ledger_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    u_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    related_entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    operation_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    estimated_tokens = table.Column<int>(type: "int", nullable: false),
                    actual_tokens = table.Column<int>(type: "int", nullable: true),
                    failure_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenLedger", x => x.ledger_id);
                    table.ForeignKey(
                        name: "FK_TokenLedger_Users_u_id",
                        column: x => x.u_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_u_id_create_at",
                table: "Notification",
                columns: new[] { "u_id", "create_at" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_u_id_is_read",
                table: "Notification",
                columns: new[] { "u_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShare_doc_id_u_id",
                table: "DocumentShare",
                columns: new[] { "doc_id", "u_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShare_u_id",
                table: "DocumentShare",
                column: "u_id");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_u_id_status",
                table: "Recommendations",
                columns: new[] { "u_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenLedger_u_id",
                table: "TokenLedger",
                column: "u_id");

            migrationBuilder.CreateIndex(
                name: "IX_TokenLedger_u_id_create_at",
                table: "TokenLedger",
                columns: new[] { "u_id", "create_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentShare");

            migrationBuilder.DropTable(
                name: "Recommendations");

            migrationBuilder.DropTable(
                name: "TokenLedger");

            migrationBuilder.DropIndex(
                name: "IX_Notification_u_id_create_at",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "IX_Notification_u_id_is_read",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "action_url",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "payload_json",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "title",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "lapses",
                table: "Flashcard");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "TrashedAt",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "TrashedBy",
                table: "Document");

            migrationBuilder.RenameColumn(
                name: "type",
                table: "Notification",
                newName: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_u_id",
                table: "Notification",
                column: "u_id");
        }
    }
}
