using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncSchemaToDbml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Answers_Questions_QuestionId",
                table: "Answers");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatSessions_ChatSessionId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_Users_UserId",
                table: "ChatSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentChunks_Documents_doc_id",
                table: "DocumentChunks");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Subjects_subject_id",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_u_id",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Flashcards_Documents_DocumentId",
                table: "Flashcards");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_TierMemberships_tier_id",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_u_id",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Quizzes_QuizId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizSubmissions_Quizzes_QuizId",
                table: "QuizSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizSubmissions_Users_UserId",
                table: "QuizSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_Documents_DocumentId",
                table: "Quizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Documents_DocumentId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Users_UserId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_TierUsers_TierMemberships_tier_id",
                table: "TierUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_TierUsers_Users_u_id",
                table: "TierUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Documents_DocumentId",
                table: "Votes");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Users_UserId",
                table: "Votes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TierUsers",
                table: "TierUsers");

            migrationBuilder.DropIndex(
                name: "IX_TierUsers_u_id_tier_id",
                table: "TierUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TierMemberships",
                table: "TierMemberships");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Reports",
                table: "Reports");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Quizzes",
                table: "Quizzes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuizSubmissions",
                table: "QuizSubmissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Questions",
                table: "Questions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Payments",
                table: "Payments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Notifications",
                table: "Notifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Flashcards",
                table: "Flashcards");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Documents",
                table: "Documents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DocumentChunks",
                table: "DocumentChunks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatSessions",
                table: "ChatSessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatMessages",
                table: "ChatMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Answers",
                table: "Answers");

            migrationBuilder.DropColumn(
                name: "tier_id",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "PassingScore",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "TimeLimitMinutes",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "description",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "file_size_bytes",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Answers");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "Answers");

            migrationBuilder.RenameTable(
                name: "TierUsers",
                newName: "TierUser");

            migrationBuilder.RenameTable(
                name: "TierMemberships",
                newName: "TierMembership");

            migrationBuilder.RenameTable(
                name: "Reports",
                newName: "Report");

            migrationBuilder.RenameTable(
                name: "Quizzes",
                newName: "Quiz");

            migrationBuilder.RenameTable(
                name: "QuizSubmissions",
                newName: "QuizSubmission");

            migrationBuilder.RenameTable(
                name: "Questions",
                newName: "Question");

            migrationBuilder.RenameTable(
                name: "Payments",
                newName: "Payment");

            migrationBuilder.RenameTable(
                name: "Notifications",
                newName: "Notification");

            migrationBuilder.RenameTable(
                name: "Flashcards",
                newName: "Flashcard");

            migrationBuilder.RenameTable(
                name: "Documents",
                newName: "Document");

            migrationBuilder.RenameTable(
                name: "DocumentChunks",
                newName: "Document_Chunk");

            migrationBuilder.RenameTable(
                name: "ChatSessions",
                newName: "ChatSession");

            migrationBuilder.RenameTable(
                name: "ChatMessages",
                newName: "ChatMessage");

            migrationBuilder.RenameTable(
                name: "Answers",
                newName: "Answer");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Votes",
                newName: "u_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Votes",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Votes",
                newName: "vote_type");

            migrationBuilder.RenameColumn(
                name: "DocumentId",
                table: "Votes",
                newName: "doc_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Votes",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Votes",
                newName: "vote_id");

            migrationBuilder.RenameIndex(
                name: "IX_Votes_UserId_DocumentId",
                table: "Votes",
                newName: "IX_Votes_u_id_doc_id");

            migrationBuilder.RenameIndex(
                name: "IX_Votes_DocumentId",
                table: "Votes",
                newName: "IX_Votes_doc_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Subjects",
                newName: "subject_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "TierUser",
                newName: "tier_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_TierUsers_tier_id",
                table: "TierUser",
                newName: "IX_TierUser_tier_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "TierMembership",
                newName: "tier_id");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "Report",
                newName: "reason");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Report",
                newName: "u_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Report",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "DocumentId",
                table: "Report",
                newName: "doc_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Report",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Report",
                newName: "report_id");

            migrationBuilder.RenameIndex(
                name: "IX_Reports_UserId",
                table: "Report",
                newName: "IX_Report_u_id");

            migrationBuilder.RenameIndex(
                name: "IX_Reports_DocumentId",
                table: "Report",
                newName: "IX_Report_doc_id");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Quiz",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Quiz",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "DocumentId",
                table: "Quiz",
                newName: "doc_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Quiz",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Quiz",
                newName: "quiz_id");

            migrationBuilder.RenameIndex(
                name: "IX_Quizzes_DocumentId",
                table: "Quiz",
                newName: "IX_Quiz_doc_id");

            migrationBuilder.RenameColumn(
                name: "Score",
                table: "QuizSubmission",
                newName: "score");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "QuizSubmission",
                newName: "u_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "QuizSubmission",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "SubmittedAt",
                table: "QuizSubmission",
                newName: "submitted_at");

            migrationBuilder.RenameColumn(
                name: "QuizId",
                table: "QuizSubmission",
                newName: "quiz_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "QuizSubmission",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "QuizSubmission",
                newName: "submission_id");

            migrationBuilder.RenameIndex(
                name: "IX_QuizSubmissions_UserId",
                table: "QuizSubmission",
                newName: "IX_QuizSubmission_u_id");

            migrationBuilder.RenameIndex(
                name: "IX_QuizSubmissions_QuizId",
                table: "QuizSubmission",
                newName: "IX_QuizSubmission_quiz_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Question",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "QuizId",
                table: "Question",
                newName: "quiz_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Question",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Question",
                newName: "question_id");

            migrationBuilder.RenameIndex(
                name: "IX_Questions_QuizId",
                table: "Question",
                newName: "IX_Question_quiz_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Payment",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Payment",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Payment",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Payment",
                newName: "payment_id");

            migrationBuilder.RenameIndex(
                name: "IX_Payments_u_id",
                table: "Payment",
                newName: "IX_Payment_u_id");

            migrationBuilder.RenameIndex(
                name: "IX_Payments_tier_id",
                table: "Payment",
                newName: "IX_Payment_tier_id");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "Notification",
                newName: "message");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Notification",
                newName: "u_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Notification",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "IsRead",
                table: "Notification",
                newName: "is_read");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Notification",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Notification",
                newName: "noti_id");

            migrationBuilder.RenameIndex(
                name: "IX_Notifications_UserId",
                table: "Notification",
                newName: "IX_Notification_u_id");

            migrationBuilder.RenameColumn(
                name: "Front",
                table: "Flashcard",
                newName: "front");

            migrationBuilder.RenameColumn(
                name: "Back",
                table: "Flashcard",
                newName: "back");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Flashcard",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "DocumentId",
                table: "Flashcard",
                newName: "doc_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Flashcard",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Flashcard",
                newName: "card_id");

            migrationBuilder.RenameIndex(
                name: "IX_Flashcards_DocumentId",
                table: "Flashcard",
                newName: "IX_Flashcard_doc_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Document",
                newName: "doc_id");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_u_id",
                table: "Document",
                newName: "IX_Document_u_id");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_subject_id",
                table: "Document",
                newName: "IX_Document_subject_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Document_Chunk",
                newName: "document_chunk_id");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentChunks_doc_id",
                table: "Document_Chunk",
                newName: "IX_Document_Chunk_doc_id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "ChatSession",
                newName: "u_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "ChatSession",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "ChatSession",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ChatSession",
                newName: "session_id");

            migrationBuilder.RenameIndex(
                name: "IX_ChatSessions_UserId",
                table: "ChatSession",
                newName: "IX_ChatSession_u_id");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "ChatMessage",
                newName: "content");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "ChatMessage",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "ChatMessage",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "ChatSessionId",
                table: "ChatMessage",
                newName: "session_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ChatMessage",
                newName: "message_id");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_ChatSessionId",
                table: "ChatMessage",
                newName: "IX_ChatMessage_session_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Answer",
                newName: "update_at");

            migrationBuilder.RenameColumn(
                name: "QuestionId",
                table: "Answer",
                newName: "question_id");

            migrationBuilder.RenameColumn(
                name: "IsCorrect",
                table: "Answer",
                newName: "is_correct");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Answer",
                newName: "create_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Answer",
                newName: "answer_id");

            migrationBuilder.RenameIndex(
                name: "IX_Answers_QuestionId",
                table: "Answer",
                newName: "IX_Answer_question_id");

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "Votes",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "vote_type",
                table: "Votes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "Votes",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "active",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Active");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Student");

            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'current_storage_capacity') IS NOT NULL
BEGIN
    DECLARE @storageDefault sysname;
    SELECT @storageDefault = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE [d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'current_storage_capacity';

    IF @storageDefault IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @storageDefault + ']');
    ALTER TABLE [Users] ADD DEFAULT 0 FOR [current_storage_capacity];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'current_ai_token_usage') IS NOT NULL
BEGIN
    DECLARE @tokenUsageDefault sysname;
    SELECT @tokenUsageDefault = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE [d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'current_ai_token_usage';

    IF @tokenUsageDefault IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @tokenUsageDefault + ']');
    ALTER TABLE [Users] ADD DEFAULT 0 FOR [current_ai_token_usage];
END
");

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "Report",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "Report",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "Report",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "Quiz",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "Quiz",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "Quiz",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<decimal>(
                name: "score",
                table: "QuizSubmission",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "QuizSubmission",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "submitted_at",
                table: "QuizSubmission",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "QuizSubmission",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "answers",
                table: "QuizSubmission",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "Question",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "Question",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "Question",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "Payment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Pending");

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "Payment",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "Payment",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<DateTime>(
                name: "payment_date",
                table: "Payment",
                type: "datetime",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "payment_info",
                table: "Payment",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "message",
                table: "Notification",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "Notification",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "is_read",
                table: "Notification",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "Notification",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "front",
                table: "Flashcard",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "back",
                table: "Flashcard",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "Flashcard",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "Flashcard",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "Document",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "subject_id",
                table: "Document",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "Document",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Draft");

            migrationBuilder.AlterColumn<string>(
                name: "file_type",
                table: "Document",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "file_link",
                table: "Document",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<string>(
                name: "file_extension",
                table: "Document",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_name",
                table: "Document",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "share_status",
                table: "Document",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "private");

            migrationBuilder.AddColumn<string>(
                name: "shared_users",
                table: "Document",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "ChatSession",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "ChatSession",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<Guid>(
                name: "doc_id",
                table: "ChatSession",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "session_title",
                table: "ChatSession",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "ChatMessage",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "ChatMessage",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "sender",
                table: "ChatMessage",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "update_at",
                table: "Answer",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "create_at",
                table: "Answer",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "selected_option",
                table: "Answer",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TierUser",
                table: "TierUser",
                column: "tier_user_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TierMembership",
                table: "TierMembership",
                column: "tier_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Report",
                table: "Report",
                column: "report_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Quiz",
                table: "Quiz",
                column: "quiz_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuizSubmission",
                table: "QuizSubmission",
                column: "submission_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Question",
                table: "Question",
                column: "question_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Payment",
                table: "Payment",
                column: "payment_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Notification",
                table: "Notification",
                column: "noti_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Flashcard",
                table: "Flashcard",
                column: "card_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Document",
                table: "Document",
                column: "doc_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Document_Chunk",
                table: "Document_Chunk",
                column: "document_chunk_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatSession",
                table: "ChatSession",
                column: "session_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatMessage",
                table: "ChatMessage",
                column: "message_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Answer",
                table: "Answer",
                column: "answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_Users_full_name",
                table: "Users",
                column: "full_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TierUser_u_id",
                table: "TierUser",
                column: "u_id");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSession_doc_id",
                table: "ChatSession",
                column: "doc_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Answer_Question_question_id",
                table: "Answer",
                column: "question_id",
                principalTable: "Question",
                principalColumn: "question_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessage_ChatSession_session_id",
                table: "ChatMessage",
                column: "session_id",
                principalTable: "ChatSession",
                principalColumn: "session_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSession_Document_doc_id",
                table: "ChatSession",
                column: "doc_id",
                principalTable: "Document",
                principalColumn: "doc_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSession_Users_u_id",
                table: "ChatSession",
                column: "u_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Document_Subjects_subject_id",
                table: "Document",
                column: "subject_id",
                principalTable: "Subjects",
                principalColumn: "subject_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Document_Users_u_id",
                table: "Document",
                column: "u_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Document_Chunk_Document_doc_id",
                table: "Document_Chunk",
                column: "doc_id",
                principalTable: "Document",
                principalColumn: "doc_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Flashcard_Document_doc_id",
                table: "Flashcard",
                column: "doc_id",
                principalTable: "Document",
                principalColumn: "doc_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_Users_u_id",
                table: "Notification",
                column: "u_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payment_TierMembership_tier_id",
                table: "Payment",
                column: "tier_id",
                principalTable: "TierMembership",
                principalColumn: "tier_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payment_Users_u_id",
                table: "Payment",
                column: "u_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Question_Quiz_quiz_id",
                table: "Question",
                column: "quiz_id",
                principalTable: "Quiz",
                principalColumn: "quiz_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quiz_Document_doc_id",
                table: "Quiz",
                column: "doc_id",
                principalTable: "Document",
                principalColumn: "doc_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizSubmission_Quiz_quiz_id",
                table: "QuizSubmission",
                column: "quiz_id",
                principalTable: "Quiz",
                principalColumn: "quiz_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizSubmission_Users_u_id",
                table: "QuizSubmission",
                column: "u_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Report_Document_doc_id",
                table: "Report",
                column: "doc_id",
                principalTable: "Document",
                principalColumn: "doc_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Report_Users_u_id",
                table: "Report",
                column: "u_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TierUser_TierMembership_tier_id",
                table: "TierUser",
                column: "tier_id",
                principalTable: "TierMembership",
                principalColumn: "tier_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TierUser_Users_u_id",
                table: "TierUser",
                column: "u_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Document_doc_id",
                table: "Votes",
                column: "doc_id",
                principalTable: "Document",
                principalColumn: "doc_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Users_u_id",
                table: "Votes",
                column: "u_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Answer_Question_question_id",
                table: "Answer");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessage_ChatSession_session_id",
                table: "ChatMessage");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatSession_Document_doc_id",
                table: "ChatSession");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatSession_Users_u_id",
                table: "ChatSession");

            migrationBuilder.DropForeignKey(
                name: "FK_Document_Subjects_subject_id",
                table: "Document");

            migrationBuilder.DropForeignKey(
                name: "FK_Document_Users_u_id",
                table: "Document");

            migrationBuilder.DropForeignKey(
                name: "FK_Document_Chunk_Document_doc_id",
                table: "Document_Chunk");

            migrationBuilder.DropForeignKey(
                name: "FK_Flashcard_Document_doc_id",
                table: "Flashcard");

            migrationBuilder.DropForeignKey(
                name: "FK_Notification_Users_u_id",
                table: "Notification");

            migrationBuilder.DropForeignKey(
                name: "FK_Payment_TierMembership_tier_id",
                table: "Payment");

            migrationBuilder.DropForeignKey(
                name: "FK_Payment_Users_u_id",
                table: "Payment");

            migrationBuilder.DropForeignKey(
                name: "FK_Question_Quiz_quiz_id",
                table: "Question");

            migrationBuilder.DropForeignKey(
                name: "FK_Quiz_Document_doc_id",
                table: "Quiz");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizSubmission_Quiz_quiz_id",
                table: "QuizSubmission");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizSubmission_Users_u_id",
                table: "QuizSubmission");

            migrationBuilder.DropForeignKey(
                name: "FK_Report_Document_doc_id",
                table: "Report");

            migrationBuilder.DropForeignKey(
                name: "FK_Report_Users_u_id",
                table: "Report");

            migrationBuilder.DropForeignKey(
                name: "FK_TierUser_TierMembership_tier_id",
                table: "TierUser");

            migrationBuilder.DropForeignKey(
                name: "FK_TierUser_Users_u_id",
                table: "TierUser");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Document_doc_id",
                table: "Votes");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Users_u_id",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Users_full_name",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TierUser",
                table: "TierUser");

            migrationBuilder.DropIndex(
                name: "IX_TierUser_u_id",
                table: "TierUser");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TierMembership",
                table: "TierMembership");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Report",
                table: "Report");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuizSubmission",
                table: "QuizSubmission");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Quiz",
                table: "Quiz");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Question",
                table: "Question");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Payment",
                table: "Payment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Notification",
                table: "Notification");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Flashcard",
                table: "Flashcard");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Document_Chunk",
                table: "Document_Chunk");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Document",
                table: "Document");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatSession",
                table: "ChatSession");

            migrationBuilder.DropIndex(
                name: "IX_ChatSession_doc_id",
                table: "ChatSession");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatMessage",
                table: "ChatMessage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Answer",
                table: "Answer");

            migrationBuilder.DropColumn(
                name: "answers",
                table: "QuizSubmission");

            migrationBuilder.DropColumn(
                name: "title",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "payment_date",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "payment_info",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "file_extension",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "file_name",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "share_status",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "shared_users",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "doc_id",
                table: "ChatSession");

            migrationBuilder.DropColumn(
                name: "session_title",
                table: "ChatSession");

            migrationBuilder.DropColumn(
                name: "sender",
                table: "ChatMessage");

            migrationBuilder.DropColumn(
                name: "selected_option",
                table: "Answer");

            migrationBuilder.RenameTable(
                name: "TierUser",
                newName: "TierUsers");

            migrationBuilder.RenameTable(
                name: "TierMembership",
                newName: "TierMemberships");

            migrationBuilder.RenameTable(
                name: "Report",
                newName: "Reports");

            migrationBuilder.RenameTable(
                name: "QuizSubmission",
                newName: "QuizSubmissions");

            migrationBuilder.RenameTable(
                name: "Quiz",
                newName: "Quizzes");

            migrationBuilder.RenameTable(
                name: "Question",
                newName: "Questions");

            migrationBuilder.RenameTable(
                name: "Payment",
                newName: "Payments");

            migrationBuilder.RenameTable(
                name: "Notification",
                newName: "Notifications");

            migrationBuilder.RenameTable(
                name: "Flashcard",
                newName: "Flashcards");

            migrationBuilder.RenameTable(
                name: "Document_Chunk",
                newName: "DocumentChunks");

            migrationBuilder.RenameTable(
                name: "Document",
                newName: "Documents");

            migrationBuilder.RenameTable(
                name: "ChatSession",
                newName: "ChatSessions");

            migrationBuilder.RenameTable(
                name: "ChatMessage",
                newName: "ChatMessages");

            migrationBuilder.RenameTable(
                name: "Answer",
                newName: "Answers");

            migrationBuilder.RenameColumn(
                name: "vote_type",
                table: "Votes",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "Votes",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "u_id",
                table: "Votes",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "doc_id",
                table: "Votes",
                newName: "DocumentId");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "Votes",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "vote_id",
                table: "Votes",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Votes_u_id_doc_id",
                table: "Votes",
                newName: "IX_Votes_UserId_DocumentId");

            migrationBuilder.RenameIndex(
                name: "IX_Votes_doc_id",
                table: "Votes",
                newName: "IX_Votes_DocumentId");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                table: "Subjects",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "tier_user_id",
                table: "TierUsers",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_TierUser_tier_id",
                table: "TierUsers",
                newName: "IX_TierUsers_tier_id");

            migrationBuilder.RenameColumn(
                name: "tier_id",
                table: "TierMemberships",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "reason",
                table: "Reports",
                newName: "Reason");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "Reports",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "u_id",
                table: "Reports",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "doc_id",
                table: "Reports",
                newName: "DocumentId");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "Reports",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "report_id",
                table: "Reports",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Report_u_id",
                table: "Reports",
                newName: "IX_Reports_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Report_doc_id",
                table: "Reports",
                newName: "IX_Reports_DocumentId");

            migrationBuilder.RenameColumn(
                name: "score",
                table: "QuizSubmissions",
                newName: "Score");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "QuizSubmissions",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "u_id",
                table: "QuizSubmissions",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "submitted_at",
                table: "QuizSubmissions",
                newName: "SubmittedAt");

            migrationBuilder.RenameColumn(
                name: "quiz_id",
                table: "QuizSubmissions",
                newName: "QuizId");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "QuizSubmissions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "submission_id",
                table: "QuizSubmissions",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_QuizSubmission_u_id",
                table: "QuizSubmissions",
                newName: "IX_QuizSubmissions_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_QuizSubmission_quiz_id",
                table: "QuizSubmissions",
                newName: "IX_QuizSubmissions_QuizId");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "Quizzes",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "Quizzes",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "doc_id",
                table: "Quizzes",
                newName: "DocumentId");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "Quizzes",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "quiz_id",
                table: "Quizzes",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Quiz_doc_id",
                table: "Quizzes",
                newName: "IX_Quizzes_DocumentId");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "Questions",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "quiz_id",
                table: "Questions",
                newName: "QuizId");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "Questions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "question_id",
                table: "Questions",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Question_quiz_id",
                table: "Questions",
                newName: "IX_Questions_QuizId");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "Payments",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "Payments",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "Payments",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "payment_id",
                table: "Payments",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Payment_u_id",
                table: "Payments",
                newName: "IX_Payments_u_id");

            migrationBuilder.RenameIndex(
                name: "IX_Payment_tier_id",
                table: "Payments",
                newName: "IX_Payments_tier_id");

            migrationBuilder.RenameColumn(
                name: "message",
                table: "Notifications",
                newName: "Message");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "Notifications",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "u_id",
                table: "Notifications",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "is_read",
                table: "Notifications",
                newName: "IsRead");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "Notifications",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "noti_id",
                table: "Notifications",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Notification_u_id",
                table: "Notifications",
                newName: "IX_Notifications_UserId");

            migrationBuilder.RenameColumn(
                name: "front",
                table: "Flashcards",
                newName: "Front");

            migrationBuilder.RenameColumn(
                name: "back",
                table: "Flashcards",
                newName: "Back");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "Flashcards",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "doc_id",
                table: "Flashcards",
                newName: "DocumentId");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "Flashcards",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "card_id",
                table: "Flashcards",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Flashcard_doc_id",
                table: "Flashcards",
                newName: "IX_Flashcards_DocumentId");

            migrationBuilder.RenameColumn(
                name: "document_chunk_id",
                table: "DocumentChunks",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Document_Chunk_doc_id",
                table: "DocumentChunks",
                newName: "IX_DocumentChunks_doc_id");

            migrationBuilder.RenameColumn(
                name: "doc_id",
                table: "Documents",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Document_u_id",
                table: "Documents",
                newName: "IX_Documents_u_id");

            migrationBuilder.RenameIndex(
                name: "IX_Document_subject_id",
                table: "Documents",
                newName: "IX_Documents_subject_id");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "ChatSessions",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "u_id",
                table: "ChatSessions",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "ChatSessions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "session_id",
                table: "ChatSessions",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_ChatSession_u_id",
                table: "ChatSessions",
                newName: "IX_ChatSessions_UserId");

            migrationBuilder.RenameColumn(
                name: "content",
                table: "ChatMessages",
                newName: "Content");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "ChatMessages",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "session_id",
                table: "ChatMessages",
                newName: "ChatSessionId");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "ChatMessages",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "message_id",
                table: "ChatMessages",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessage_session_id",
                table: "ChatMessages",
                newName: "IX_ChatMessages_ChatSessionId");

            migrationBuilder.RenameColumn(
                name: "update_at",
                table: "Answers",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "question_id",
                table: "Answers",
                newName: "QuestionId");

            migrationBuilder.RenameColumn(
                name: "is_correct",
                table: "Answers",
                newName: "IsCorrect");

            migrationBuilder.RenameColumn(
                name: "create_at",
                table: "Answers",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "answer_id",
                table: "Answers",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Answer_question_id",
                table: "Answers",
                newName: "IX_Answers_QuestionId");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Votes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Votes",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Votes",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "active");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Student",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.Sql(@"
DECLARE @storageDefault sysname;
SELECT @storageDefault = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE [d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'current_storage_capacity';

IF @storageDefault IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @storageDefault + ']');
");

            migrationBuilder.Sql(@"
DECLARE @tokenUsageDefault sysname;
SELECT @tokenUsageDefault = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE [d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'current_ai_token_usage';

IF @tokenUsageDefault IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @tokenUsageDefault + ']');
");

            migrationBuilder.AddColumn<Guid>(
                name: "tier_id",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "Reports",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Reports",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Reports",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "Reports",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Reports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AlterColumn<decimal>(
                name: "Score",
                table: "QuizSubmissions",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "QuizSubmissions",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmittedAt",
                table: "QuizSubmissions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "QuizSubmissions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Quizzes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Quizzes",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Quizzes",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Quizzes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PassingScore",
                table: "Quizzes",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitMinutes",
                table: "Quizzes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Questions",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Questions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<decimal>(
                name: "Points",
                table: "Questions",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Questions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "Questions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Questions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Payments",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Payments",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "Payments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Payments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionId",
                table: "Payments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Notifications",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsRead",
                table: "Notifications",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Notifications",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Front",
                table: "Flashcards",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Back",
                table: "Flashcards",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Flashcards",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Flashcards",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Flashcards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "Documents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<Guid>(
                name: "subject_id",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "Documents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Draft",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "file_type",
                table: "Documents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "file_link",
                table: "Documents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "Documents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "file_size_bytes",
                table: "Documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ChatSessions",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ChatSessions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ChatSessions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ChatMessages",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "ChatMessages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Answers",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Answers",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Answers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "Answers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TierUsers",
                table: "TierUsers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TierMemberships",
                table: "TierMemberships",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Reports",
                table: "Reports",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuizSubmissions",
                table: "QuizSubmissions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Quizzes",
                table: "Quizzes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Questions",
                table: "Questions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Payments",
                table: "Payments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Notifications",
                table: "Notifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Flashcards",
                table: "Flashcards",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DocumentChunks",
                table: "DocumentChunks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Documents",
                table: "Documents",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatSessions",
                table: "ChatSessions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatMessages",
                table: "ChatMessages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Answers",
                table: "Answers",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TierUsers_u_id_tier_id",
                table: "TierUsers",
                columns: new[] { "u_id", "tier_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Answers_Questions_QuestionId",
                table: "Answers",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatSessions_ChatSessionId",
                table: "ChatMessages",
                column: "ChatSessionId",
                principalTable: "ChatSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSessions_Users_UserId",
                table: "ChatSessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentChunks_Documents_doc_id",
                table: "DocumentChunks",
                column: "doc_id",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_Flashcards_Documents_DocumentId",
                table: "Flashcards",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

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

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Quizzes_QuizId",
                table: "Questions",
                column: "QuizId",
                principalTable: "Quizzes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizSubmissions_Quizzes_QuizId",
                table: "QuizSubmissions",
                column: "QuizId",
                principalTable: "Quizzes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizSubmissions_Users_UserId",
                table: "QuizSubmissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_Documents_DocumentId",
                table: "Quizzes",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Documents_DocumentId",
                table: "Reports",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Users_UserId",
                table: "Reports",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TierUsers_TierMemberships_tier_id",
                table: "TierUsers",
                column: "tier_id",
                principalTable: "TierMemberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TierUsers_Users_u_id",
                table: "TierUsers",
                column: "u_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Documents_DocumentId",
                table: "Votes",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Users_UserId",
                table: "Votes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
