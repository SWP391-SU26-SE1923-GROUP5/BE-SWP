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
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasIndex(x => x.SubjectCode).IsUnique();
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
        builder.Property(x => x.SharedUsers).HasColumnName("shared_users");
        builder.Property(x => x.ShareStatus).HasColumnName("share_status").HasMaxLength(20).HasDefaultValue("private");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.User).WithMany(x => x.Documents).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany(x => x.Documents).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("Document_Chunk");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("document_chunk_id");
        builder.Property(x => x.DocumentId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.ChunkJson).HasColumnName("chunk_json");
        builder.Property(x => x.EmbeddingJson).HasColumnName("embedding_json");
        builder.Property(x => x.VectorId).HasColumnName("vector_id").HasMaxLength(500);
        builder.Property(x => x.OrderIndex).HasColumnName("order_index");
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.Document).WithMany(x => x.DocumentChunks).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
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
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.User).WithMany(x => x.Reports).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Document).WithMany(x => x.Reports).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
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
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.Document).WithMany(x => x.Flashcards).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
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
        builder.Property(x => x.Message).HasColumnName("message").IsRequired();
        builder.Property(x => x.IsRead).HasColumnName("is_read").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
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

internal sealed class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        builder.ToTable("ChatSession");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("session_id");
        builder.Property(x => x.UserId).HasColumnName("u_id").IsRequired();
        builder.Property(x => x.DocumentId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.SessionTitle).HasColumnName("session_title").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.User).WithMany(x => x.ChatSessions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Document).WithMany(x => x.ChatSessions).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
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
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.ChatSession).WithMany(x => x.ChatMessages).HasForeignKey(x => x.ChatSessionId).OnDelete(DeleteBehavior.Cascade);
    }
}
