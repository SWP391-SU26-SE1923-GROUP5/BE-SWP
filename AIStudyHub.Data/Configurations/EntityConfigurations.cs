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
        builder.Property(x => x.TierId).HasColumnName("tier_id");
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.Property(x => x.CurrentStorageCapacity).HasColumnName("current_storage_capacity");
        builder.Property(x => x.CurrentAiToken).HasColumnName("current_ai_token");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Active");
        builder.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).HasDefaultValue(UserRole.Student);
        builder.HasIndex(x => x.Email).IsUnique();
    }
}

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.FileUrl).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).HasDefaultValue(DocumentStatus.Draft);
        builder.HasOne(x => x.User).WithMany(x => x.Documents).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("Votes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.DocumentId }).IsUnique();
        builder.HasOne(x => x.User).WithMany(x => x.Votes).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Document).WithMany(x => x.Votes).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Details).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).HasDefaultValue(ReportStatus.Pending);
        builder.HasOne(x => x.User).WithMany(x => x.Reports).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Document).WithMany(x => x.Reports).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FlashcardConfiguration : IEntityTypeConfiguration<Flashcard>
{
    public void Configure(EntityTypeBuilder<Flashcard> builder)
    {
        builder.ToTable("Flashcards");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Front).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Back).HasMaxLength(4000).IsRequired();
        builder.HasOne(x => x.Document).WithMany(x => x.Flashcards).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("Quizzes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.PassingScore).HasPrecision(5, 2);
        builder.HasOne(x => x.Document).WithMany(x => x.Quizzes).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Points).HasPrecision(6, 2);
        builder.HasOne(x => x.Quiz).WithMany(x => x.Questions).HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.ToTable("Answers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        builder.HasOne(x => x.Question).WithMany(x => x.Answers).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuizSubmissionConfiguration : IEntityTypeConfiguration<QuizSubmission>
{
    public void Configure(EntityTypeBuilder<QuizSubmission> builder)
    {
        builder.ToTable("QuizSubmissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Score).HasPrecision(5, 2);
        builder.HasOne(x => x.User).WithMany(x => x.QuizSubmissions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Quiz).WithMany(x => x.QuizSubmissions).HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).HasDefaultValue(PaymentStatus.Pending);
        builder.HasOne(x => x.User).WithMany(x => x.Payments).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        builder.ToTable("ChatSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.User).WithMany(x => x.ChatSessions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Content).HasMaxLength(8000).IsRequired();
        builder.HasOne(x => x.ChatSession).WithMany(x => x.ChatMessages).HasForeignKey(x => x.ChatSessionId).OnDelete(DeleteBehavior.Cascade);
    }
}
