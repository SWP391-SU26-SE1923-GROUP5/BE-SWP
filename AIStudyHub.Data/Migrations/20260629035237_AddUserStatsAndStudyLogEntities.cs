using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStatsAndStudyLogEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserStats",
                columns: table => new
                {
                    stats_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    u_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    total_xp = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    current_level = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    current_streak = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    best_streak = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    last_activity_date = table.Column<DateTime>(type: "datetime", nullable: true),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStats", x => x.stats_id);
                    table.ForeignKey(
                        name: "FK_UserStats_Users_u_id",
                        column: x => x.u_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserStats_u_id",
                table: "UserStats",
                column: "u_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserStats_CurrentLevel_TotalXp",
                table: "UserStats",
                columns: new[] { "current_level", "total_xp" });

            migrationBuilder.CreateTable(
                name: "StudyLogs",
                columns: table => new
                {
                    log_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    u_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activity_type = table.Column<int>(type: "int", nullable: false),
                    doc_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    subject_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    is_correct = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    time_spent_seconds = table.Column<int>(type: "int", nullable: true),
                    xp_earned = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    create_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    update_at = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyLogs", x => x.log_id);
                    table.ForeignKey(
                        name: "FK_StudyLogs_Users_u_id",
                        column: x => x.u_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyLogs_Document_doc_id",
                        column: x => x.doc_id,
                        principalTable: "Document",
                        principalColumn: "doc_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudyLogs_doc_id",
                table: "StudyLogs",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_StudyLogs_UserId_SubjectCode",
                table: "StudyLogs",
                columns: new[] { "u_id", "subject_code" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyLogs_UserId_CreatedAt",
                table: "StudyLogs",
                columns: new[] { "u_id", "create_at" });

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "TierMembership",
                newName: "price");

            migrationBuilder.AlterColumn<decimal>(
                name: "price",
                table: "TierMembership",
                type: "decimal(18,0)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                table: "Report",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "category",
                table: "Report",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 5);

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "create_at",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudyLogs");

            migrationBuilder.DropTable(
                name: "UserStats");

            migrationBuilder.RenameColumn(
                name: "price",
                table: "TierMembership",
                newName: "Price");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "TierMembership",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                table: "Report",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "category",
                table: "Report",
                type: "int",
                nullable: false,
                defaultValue: 5,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 28, 16, 4, 57, 970, DateTimeKind.Utc).AddTicks(1050));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "create_at",
                value: new DateTime(2026, 6, 28, 16, 4, 57, 970, DateTimeKind.Utc).AddTicks(1056));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 28, 16, 4, 57, 970, DateTimeKind.Utc).AddTicks(1058));
        }
    }
}
