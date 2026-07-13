using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentShareRecommendationTokenWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: all schema changes in this migration were already applied
            // by the preceding partial run of FixDocumentShareUserTableName.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Document_Subjects_subject_id",
                table: "Document");

            migrationBuilder.DropForeignKey(
                name: "FK_Document_Users_u_id",
                table: "Document");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentShare_Document_doc_id",
                table: "DocumentShare");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentShare_Users_u_id",
                table: "DocumentShare");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Users_u_id",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_TokenLedger_Users_u_id",
                table: "TokenLedger");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_TierMembership_tier_id",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Answer");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "ChatMessage");

            migrationBuilder.DropTable(
                name: "ChatSessionDocument");

            migrationBuilder.DropTable(
                name: "FlashcardReviews");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "OtpRecords");

            migrationBuilder.DropTable(
                name: "Payment");

            migrationBuilder.DropTable(
                name: "QuizSubmission");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Report");

            migrationBuilder.DropTable(
                name: "StudyLogs");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropTable(
                name: "UserBadge");

            migrationBuilder.DropTable(
                name: "UserStats");

            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropTable(
                name: "Question");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "ChatSession");

            migrationBuilder.DropTable(
                name: "Flashcard");

            migrationBuilder.DropTable(
                name: "TierMembership");

            migrationBuilder.DropTable(
                name: "Badge");

            migrationBuilder.DropTable(
                name: "Quiz");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Document",
                table: "Document");

            migrationBuilder.DropIndex(
                name: "IX_Document_subject_id",
                table: "Document");

            migrationBuilder.DropIndex(
                name: "IX_Document_u_id",
                table: "Document");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_mail",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_tier_id",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "create_at",
                table: "DocumentShare");

            migrationBuilder.DropColumn(
                name: "update_at",
                table: "DocumentShare");

            migrationBuilder.DropColumn(
                name: "doc_id",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "IsOcrApplied",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "TrashedAt",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "TrashedBy",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "create_at",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "file_extension",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "file_link",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "file_name",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "file_type",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "is_non_flaggable",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "share_status",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "shared_users",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "status",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "subject_id",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "title",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "update_at",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedUserName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneNumberConfirmed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "create_at",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "current_ai_token_usage",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "current_storage_capacity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "dob",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "full_name",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "mail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "status",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "tier_expire_at",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "tier_id",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "update_at",
                table: "Users");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "User");

            migrationBuilder.RenameColumn(
                name: "u_id",
                table: "Document",
                newName: "TempId1");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "User",
                newName: "TempId3");

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "TokenLedger",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "Recommendations",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<Guid>(
                name: "TempId1",
                table: "User",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TempId2",
                table: "User",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Document_TempId1",
                table: "Document",
                column: "TempId1");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_User_TempId1",
                table: "User",
                column: "TempId1");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_User_TempId2",
                table: "User",
                column: "TempId2");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_User_TempId3",
                table: "User",
                column: "TempId3");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentShare_Document_doc_id",
                table: "DocumentShare",
                column: "doc_id",
                principalTable: "Document",
                principalColumn: "TempId1",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentShare_User_u_id",
                table: "DocumentShare",
                column: "u_id",
                principalTable: "User",
                principalColumn: "TempId1",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_User_u_id",
                table: "Recommendations",
                column: "u_id",
                principalTable: "User",
                principalColumn: "TempId3",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TokenLedger_User_u_id",
                table: "TokenLedger",
                column: "u_id",
                principalTable: "User",
                principalColumn: "TempId2",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
