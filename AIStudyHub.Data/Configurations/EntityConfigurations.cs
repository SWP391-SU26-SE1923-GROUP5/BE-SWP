using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIStudyHub.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Email).HasColumnName("mail").HasMaxLength(255).IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        builder.Property(x => x.DateOfBirth).HasColumnName("dob").HasColumnType("date");
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.Property(x => x.CurrentStorageCapacity).HasColumnName("current_storage_capacity").HasDefaultValue(0);
        builder.Property(x => x.CurrentAiTokenUsage).HasColumnName("current_ai_token_usage").HasDefaultValue(0);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("active");
        builder.Property(x => x.Role).HasColumnName("role").HasMaxLength(20).IsRequired();
        builder.Property(x => x.TierId).HasColumnName("tier_id").IsRequired().HasDefaultValue(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        builder.Property(x => x.TierExpireAt).HasColumnName("tier_expire_at").HasColumnType("datetime");
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasOne(x => x.TierMembership).WithMany(x => x.Users).HasForeignKey(x => x.TierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.DocumentShares).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(x => x.ExpiresAt).HasColumnType("datetime").IsRequired();
        builder.Property(x => x.RevokedAt).HasColumnType("datetime");
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("subject_id");
        builder.Property(x => x.SubjectCode).HasColumnName("subject_code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SubjectName).HasColumnName("subject_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.OwnerUserId)
            .HasColumnName("owner_user_id")
            .IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasIndex(x => new { x.OwnerUserId, x.SubjectCode })
            .IsUnique();
        builder.HasOne(x => x.OwnerUser)
            .WithMany(x => x.Subjects)
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TierMembershipConfiguration : IEntityTypeConfiguration<TierMembership>
{
    public void Configure(EntityTypeBuilder<TierMembership> builder)
    {
        builder.ToTable("TierMembership");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("tier_id");
        builder.Property(x => x.TierName).HasColumnName("tier_name").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Price).HasColumnName("price").HasColumnType("decimal(18,0)").IsRequired();
        builder.Property(x => x.StorageLimitMb).HasColumnName("storage_limit_mb").IsRequired();
        builder.Property(x => x.AiTokens).HasColumnName("ai_tokens").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
    }
}

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Document");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("doc_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        builder.Property(x => x.FileLink).HasColumnName("file_link");
        builder.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255);
        builder.Property(x => x.FileExtension).HasColumnName("file_extension").HasMaxLength(255);
        builder.Property(x => x.FileType).HasColumnName("file_type").HasMaxLength(128);
        builder.Property(x => x.ShareStatus).HasColumnName("share_status").HasMaxLength(20).HasDefaultValue("private");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IsNonFlaggable).HasColumnName("is_non_flaggable").HasDefaultValue(false);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message");
        builder.Property(x => x.ProcessingVersion).HasColumnName("processing_version").HasDefaultValue(1);
        builder.Property(x => x.ReindexClaimId).HasColumnName("reindex_claim_id");
        builder.Property(x => x.ReindexClaimedAt).HasColumnName("reindex_claimed_at").HasColumnType("datetime");
        builder.Property(x => x.ReindexAttemptCount).HasColumnName("reindex_attempt_count").HasDefaultValue(0);
        builder.Property(x => x.LastReindexError).HasColumnName("last_reindex_error");
        builder.Property(x => x.SuggestedPromptsJson).HasColumnName("suggested_prompts_json");
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasIndex(x => new { x.UserId, x.FileName })
            .HasDatabaseName("UX_Document_UserId_FileName_Active")
            .IsUnique()
            .HasFilter("[LifecycleStatus] = 0 AND [file_name] IS NOT NULL");
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.ProcessingVersion, x.ReindexClaimedAt })
            .HasDatabaseName("IX_Document_ReindexEligibility");
        builder.HasOne(x => x.User).WithMany(x => x.Documents).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany(x => x.Documents).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("Votes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("vote_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.DocumentId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.Type).HasColumnName("vote_type").HasConversion(
            x => x == VoteType.Upvote ? "up" : "down",
            x => x == "down" ? VoteType.Downvote : VoteType.Upvote).HasMaxLength(10).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasIndex(x => new { x.UserId, x.DocumentId }).IsUnique();
        builder.HasOne(x => x.User).WithMany(x => x.Votes).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Document).WithMany(x => x.Votes).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Report");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("report_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.DocumentId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasSentinel((ReportCategory)(-1)).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.Status).HasColumnName("status").HasSentinel((ReportStatus)(-1)).IsRequired();
        builder.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at").HasColumnType("datetime2");
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.User).WithMany(x => x.Reports).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Document).WithMany(x => x.Reports).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ResolvedByUser).WithMany().HasForeignKey(x => x.ResolvedBy).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(x => new { x.UserId, x.DocumentId })
               .IsUnique()
               .HasDatabaseName("IX_Reports_UserId_DocumentId_Pending")
               .HasFilter("status = 1");
    }
}

internal sealed class FlashcardConfiguration : IEntityTypeConfiguration<Flashcard>
{
    public void Configure(EntityTypeBuilder<Flashcard> builder)
    {
        builder.ToTable("Flashcard");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("card_id");
        builder.Property(x => x.DocumentId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.Front).HasColumnName("front").IsRequired();
        builder.Property(x => x.Back).HasColumnName("back").IsRequired();
        builder.Property(x => x.Lapses).HasColumnName("lapses").IsRequired().HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.Document).WithMany(x => x.Flashcards).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FlashcardReviewConfiguration : IEntityTypeConfiguration<FlashcardReview>
{
    public void Configure(EntityTypeBuilder<FlashcardReview> builder)
    {
        builder.ToTable("FlashcardReviews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("review_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.FlashcardId).HasColumnName("card_id").IsRequired();
        builder.Property(x => x.EaseFactor).HasColumnName("ease_factor").IsRequired();
        builder.Property(x => x.Interval).HasColumnName("interval_days").IsRequired();
        builder.Property(x => x.Repetitions).HasColumnName("repetitions").IsRequired();
        builder.Property(x => x.NextReviewDate).HasColumnName("next_review_date").HasColumnType("datetime").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");

        // One review row per (user, flashcard)
        builder.HasIndex(x => new { x.UserId, x.FlashcardId }).IsUnique();

        // Hot path: "due today" query
        builder.HasIndex(x => new { x.UserId, x.NextReviewDate });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Flashcard)
            .WithMany()
            .HasForeignKey(x => x.FlashcardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FlashcardReviewAttemptConfiguration : IEntityTypeConfiguration<FlashcardReviewAttempt>
{
    public void Configure(EntityTypeBuilder<FlashcardReviewAttempt> builder)
    {
        builder.ToTable("FlashcardReviewAttempt");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("attempt_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.FlashcardId).HasColumnName("card_id").IsRequired();
        builder.Property(x => x.Quality)
            .HasColumnName("quality")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.TimeSpentSeconds).HasColumnName("time_spent_seconds");
        builder.Property(x => x.PreviousEaseFactor).HasColumnName("previous_ease_factor").IsRequired();
        builder.Property(x => x.ResultEaseFactor).HasColumnName("result_ease_factor").IsRequired();
        builder.Property(x => x.PreviousInterval).HasColumnName("previous_interval").IsRequired();
        builder.Property(x => x.ResultInterval).HasColumnName("result_interval").IsRequired();
        builder.Property(x => x.PreviousRepetitions).HasColumnName("previous_repetitions").IsRequired();
        builder.Property(x => x.ResultRepetitions).HasColumnName("result_repetitions").IsRequired();
        builder.Property(x => x.PreviousNextReviewDate)
            .HasColumnName("previous_next_review_date")
            .HasColumnType("datetime")
            .IsRequired();
        builder.Property(x => x.ResultNextReviewDate)
            .HasColumnName("result_next_review_date")
            .HasColumnType("datetime")
            .IsRequired();
        builder.Property(x => x.XpEarned).HasColumnName("xp_earned").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");

        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => new { x.UserId, x.FlashcardId, x.CreatedAt });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Flashcard)
            .WithMany()
            .HasForeignKey(x => x.FlashcardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserStatsConfiguration : IEntityTypeConfiguration<UserStats>
{
    public void Configure(EntityTypeBuilder<UserStats> builder)
    {
        builder.ToTable("UserStats");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("stats_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.TotalXp).HasColumnName("total_xp").HasDefaultValue(0);
        builder.Property(x => x.CurrentLevel).HasColumnName("current_level").HasDefaultValue(1);
        builder.Property(x => x.CurrentStreak).HasColumnName("current_streak").HasDefaultValue(0);
        builder.Property(x => x.BestStreak).HasColumnName("best_streak").HasDefaultValue(0);
        builder.Property(x => x.LastActivityDate).HasColumnName("last_activity_date").HasColumnType("datetime");
        builder.Property(x => x.TotalStudySeconds).HasColumnName("total_study_seconds").HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");

        // One row per user
        builder.HasIndex(x => x.UserId).IsUnique();

        // Leaderboard index
        builder.HasIndex(x => new { x.CurrentLevel, x.TotalXp });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StudyLogConfiguration : IEntityTypeConfiguration<StudyLog>
{
    public void Configure(EntityTypeBuilder<StudyLog> builder)
    {
        builder.ToTable("StudyLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("log_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.ActivityType).HasColumnName("activity_type").HasConversion<int>().IsRequired();
        builder.Property(x => x.DocumentId).HasColumnName("doc_id");
        builder.Property(x => x.SubjectCode).HasColumnName("subject_code").HasMaxLength(20);
        builder.Property(x => x.IsCorrect).HasColumnName("is_correct").HasDefaultValue(false);
        builder.Property(x => x.TimeSpentSeconds).HasColumnName("time_spent_seconds");
        builder.Property(x => x.XpEarned).HasColumnName("xp_earned").HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");

        // Hot path: GROUP BY subject per user (mastery analytics)
        builder.HasIndex(x => new { x.UserId, x.SubjectCode });

        // Time-range scans (charts, daily streaks)
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("Quiz");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("quiz_id");
        builder.Property(x => x.DocumentId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.Document).WithMany(x => x.Quizzes).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Question");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("question_id");
        builder.Property(x => x.QuizId).HasColumnName("quiz_id").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.Quiz).WithMany(x => x.Questions).HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.ToTable("Answer");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("answer_id");
        builder.Property(x => x.QuestionId).HasColumnName("question_id").IsRequired();
        builder.Property(x => x.SelectedOption).HasColumnName("selected_option").IsRequired();
        builder.Property(x => x.IsCorrect).HasColumnName("is_correct").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.Question).WithMany(x => x.Answers).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuizSubmissionConfiguration : IEntityTypeConfiguration<QuizSubmission>
{
    public void Configure(EntityTypeBuilder<QuizSubmission> builder)
    {
        builder.ToTable("QuizSubmission");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("submission_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.QuizId).HasColumnName("quiz_id").IsRequired();
        builder.Property(x => x.Answers).HasColumnName("answers").IsRequired();
        builder.Property(x => x.Score).HasColumnName("score").HasPrecision(5, 2);
        builder.Property(x => x.DurationSeconds).HasColumnName("duration_seconds");
        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at").HasColumnType("datetime");
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.User).WithMany(x => x.QuizSubmissions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Quiz).WithMany(x => x.QuizSubmissions).HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("noti_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(x => x.Message).HasColumnName("message").IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json");
        builder.Property(x => x.ActionUrl).HasColumnName("action_url").HasMaxLength(500);
        builder.Property(x => x.IsRead).HasColumnName("is_read").HasDefaultValue(false);
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.UserId, x.IsRead });
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("payment_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.PaymentInfo).HasColumnName("payment_info").IsRequired();
        builder.Property(x => x.PaymentDate).HasColumnName("payment_date").HasColumnType("datetime");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TierId).HasColumnName("tier_id");
        builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)");
        builder.Property(x => x.TransactionId).HasColumnName("transaction_id").HasMaxLength(100);
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.User).WithMany(x => x.Payments).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TierMembership).WithMany(x => x.Payments).HasForeignKey(x => x.TierId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ChatSessionDocumentConfiguration : IEntityTypeConfiguration<ChatSessionDocument>
{
    public void Configure(EntityTypeBuilder<ChatSessionDocument> builder)
    {
        builder.ToTable("ChatSessionDocument");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ChatSessionId).HasColumnName("session_id").IsRequired();
        builder.Property(x => x.DocumentId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.ChatSession)
            .WithMany(x => x.ChatSessionDocuments)
            .HasForeignKey(x => x.ChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Document)
            .WithMany(x => x.ChatSessionDocuments)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ChatSessionId, x.DocumentId }).IsUnique();
    }
}

internal sealed class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        builder.ToTable("ChatSession");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("session_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.SessionTitle).HasColumnName("session_title").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.User).WithMany(x => x.ChatSessions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.ChatSessionDocuments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessage");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("message_id");
        builder.Property(x => x.ChatSessionId).HasColumnName("session_id").IsRequired();
        builder.Property(x => x.Sender).HasColumnName("sender").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").IsRequired();
        builder.Property(x => x.IsRelevant).HasColumnName("is_relevant").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.ChatSession).WithMany(x => x.ChatMessages).HasForeignKey(x => x.ChatSessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> builder)
    {
        builder.ToTable("Badge");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("badge_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(50).IsRequired();
        builder.Property(x => x.TargetValue).HasColumnName("target_value").HasColumnType("decimal(18,2)");
        builder.Property(x => x.IconUrl).HasColumnName("icon_url").HasMaxLength(500);
        builder.Property(x => x.XpReward).HasColumnName("xp_reward").HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class UserBadgeConfiguration : IEntityTypeConfiguration<UserBadge>
{
    public void Configure(EntityTypeBuilder<UserBadge> builder)
    {
        builder.ToTable("UserBadge");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("user_badge_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.BadgeId).HasColumnName("badge_id").IsRequired();
        builder.Property(x => x.EarnedDate).HasColumnName("earned_date").HasColumnType("datetime");
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");

        // Idempotency: a user can earn a given badge at most once
        builder.HasIndex(x => new { x.UserId, x.BadgeId }).IsUnique();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Badge)
            .WithMany(x => x.UserBadges)
            .HasForeignKey(x => x.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TokenLedgerConfiguration : IEntityTypeConfiguration<TokenLedger>
{
    public void Configure(EntityTypeBuilder<TokenLedger> builder)
    {
        builder.ToTable("TokenLedger");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ledger_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.RelatedEntityId).HasColumnName("related_entity_id");
        builder.Property(x => x.OperationType).HasColumnName("operation_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.EstimatedTokens).HasColumnName("estimated_tokens").IsRequired();
        builder.Property(x => x.ActualTokens).HasColumnName("actual_tokens");
        builder.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("Recommendations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("recommendation_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        builder.Property(x => x.ReferenceId).HasColumnName("reference_id");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").IsRequired();
        builder.Property(x => x.ActionUrl).HasColumnName("action_url").HasMaxLength(500);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Active");
        builder.Property(x => x.DismissedAt).HasColumnName("dismissed_at").HasColumnType("datetime");
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");

        builder.HasIndex(x => new { x.UserId, x.Status });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DocumentShareConfiguration : IEntityTypeConfiguration<DocumentShare>
{
    public void Configure(EntityTypeBuilder<DocumentShare> builder)
    {
        builder.ToTable("DocumentShare");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("share_id");
        builder.Property(x => x.DocumentId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.Level).HasColumnName("level").HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(x => x.SharedBy).HasColumnName("shared_by").IsRequired();
        builder.Property(x => x.SharedAt).HasColumnName("shared_at").HasColumnType("datetime");
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");

        builder.HasIndex(x => new { x.DocumentId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.HasOne(x => x.Document).WithMany(x => x.DocumentShares).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
