using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase1_Phase8Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "score",
                table: "QuizSubmission",
                type: "int",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GradedAt",
                table: "QuizSubmission",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxScore",
                table: "QuizSubmission",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalCorrect",
                table: "QuizSubmission",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Notification",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "Document",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 17, 5, 56, 15, 789, DateTimeKind.Utc).AddTicks(6066));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 17, 5, 56, 15, 789, DateTimeKind.Utc).AddTicks(6070));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GradedAt",
                table: "QuizSubmission");

            migrationBuilder.DropColumn(
                name: "MaxScore",
                table: "QuizSubmission");

            migrationBuilder.DropColumn(
                name: "TotalCorrect",
                table: "QuizSubmission");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "Document");

            migrationBuilder.AlterColumn<decimal>(
                name: "score",
                table: "QuizSubmission",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 15, 16, 19, 14, 796, DateTimeKind.Utc).AddTicks(3932));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 15, 16, 19, 14, 796, DateTimeKind.Utc).AddTicks(3935));
        }
    }
}
