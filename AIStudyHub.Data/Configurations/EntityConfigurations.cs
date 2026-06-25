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

        // Hardcode subjects based on user requirement
        var now = new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new Subject { Id = Guid.Parse("3d093807-a8d5-4a51-aa77-635a5548ad58"), SubjectCode = "CS", SubjectName = "Computer Science", Description = "Documents related to algorithms, programming languages, software development, and computing theory.", CreatedAt = now },
            new Subject { Id = Guid.Parse("63be61df-3336-4d71-aac4-c4e03f77337a"), SubjectCode = "MATH", SubjectName = "Mathematics", Description = "Materials covering algebra, calculus, geometry, statistics, and applied mathematics.", CreatedAt = now },
            new Subject { Id = Guid.Parse("aadad9a0-a847-4437-8c1a-2443ef5c4543"), SubjectCode = "PHYS", SubjectName = "Physics", Description = "Study materials for classical mechanics, electromagnetism, thermodynamics, and quantum physics.", CreatedAt = now },
            new Subject { Id = Guid.Parse("f6dd951c-b8e4-4f41-b6e6-ad04707e6c61"), SubjectCode = "CHEM", SubjectName = "Chemistry", Description = "Resources on organic, inorganic, physical chemistry, and chemical reactions.", CreatedAt = now },
            new Subject { Id = Guid.Parse("8a716c82-f3de-472c-ab02-acf5e9fd51d6"), SubjectCode = "BIO", SubjectName = "Biology", Description = "Documents about genetics, anatomy, ecology, botany, and zoology.", CreatedAt = now },
            new Subject { Id = Guid.Parse("a2af6d40-1d8c-4940-bd0e-13629c85a480"), SubjectCode = "ECON", SubjectName = "Economics", Description = "Materials discussing microeconomics, macroeconomics, market behavior, and economic policies.", CreatedAt = now },
            new Subject { Id = Guid.Parse("955e64cf-01ad-42b1-9e2f-3176527c0eaa"), SubjectCode = "BUS", SubjectName = "Business", Description = "Resources on management, entrepreneurship, corporate strategy, and organizational behavior.", CreatedAt = now },
            new Subject { Id = Guid.Parse("54084e95-a302-4c09-bbf2-5fb34f6b5b2e"), SubjectCode = "ACC", SubjectName = "Accounting", Description = "Documents related to financial reporting, auditing, taxation, and bookkeeping.", CreatedAt = now },
            new Subject { Id = Guid.Parse("3d9d068a-bf45-48e3-80b4-ad2c438354e0"), SubjectCode = "FIN", SubjectName = "Finance", Description = "Materials on investments, corporate finance, financial markets, and wealth management.", CreatedAt = now },
            new Subject { Id = Guid.Parse("7217e917-a145-487f-b597-d2066d7f9ec9"), SubjectCode = "MKT", SubjectName = "Marketing", Description = "Resources covering consumer behavior, market research, advertising, and digital marketing.", CreatedAt = now },
            new Subject { Id = Guid.Parse("8e159166-e735-4db5-a32d-5931f8401483"), SubjectCode = "LAW", SubjectName = "Law", Description = "Documents related to legal systems, civil rights, corporate law, and criminal justice.", CreatedAt = now },
            new Subject { Id = Guid.Parse("c5790423-d558-4347-bf0b-39f6addfe9fb"), SubjectCode = "MED", SubjectName = "Medicine", Description = "Materials covering human health, diseases, pharmacology, and clinical practices.", CreatedAt = now },
            new Subject { Id = Guid.Parse("a8c0cdcd-7939-44f1-887a-1a9ebc70b9ad"), SubjectCode = "ENG", SubjectName = "English", Description = "Resources for English literature, grammar, linguistics, and writing skills.", CreatedAt = now },
            new Subject { Id = Guid.Parse("88ee3df8-aeea-4440-b610-74f7ba55ac9e"), SubjectCode = "HIST", SubjectName = "History", Description = "Documents about past events, ancient civilizations, world wars, and historical analysis.", CreatedAt = now },
            new Subject { Id = Guid.Parse("dfe0ea06-daa1-469d-b4cf-0774c3bac0c5"), SubjectCode = "GEO", SubjectName = "Geography", Description = "Materials on physical environments, human geography, maps, and earth sciences.", CreatedAt = now },
            new Subject { Id = Guid.Parse("a611af01-0c29-45af-9d1a-ad792d97e863"), SubjectCode = "PSY", SubjectName = "Psychology", Description = "Study materials on human behavior, cognitive processes, and mental health.", CreatedAt = now },
            new Subject { Id = Guid.Parse("9fad3be5-9e10-4d1b-b2de-55e585c7f98c"), SubjectCode = "ENGR", SubjectName = "Engineering", Description = "Resources for civil, mechanical, electrical, and other engineering disciplines.", CreatedAt = now },
            new Subject { Id = Guid.Parse("8ca7c447-5702-4e45-b4af-406953ab030d"), SubjectCode = "AI", SubjectName = "Artificial Intelligence", Description = "Documents on machine learning, neural networks, robotics, and natural language processing.", CreatedAt = now },
            new Subject { Id = Guid.Parse("9ee16682-c880-4074-8bf7-c9299e690d76"), SubjectCode = "DS", SubjectName = "Data Science", Description = "Materials covering data analysis, big data, data visualization, and statistical modeling.", CreatedAt = now },
            new Subject { Id = Guid.Parse("4d6dd566-14a4-46e8-9c1f-e7b064b41354"), SubjectCode = "CYBER", SubjectName = "Cybersecurity", Description = "Resources on information security, cryptography, network protection, and ethical hacking.", CreatedAt = now },
            new Subject { Id = Guid.Parse("9c12d917-5c56-4238-b96b-d1a3c831bf40"), SubjectCode = "OTHER", SubjectName = "Other", Description = "For any document that does not fit into the predefined categories above.", CreatedAt = now }
        );
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
        builder.Property(x => x.DocumentId).HasColumnName("doc_id");
        builder.Property(x => x.SessionTitle).HasColumnName("session_title").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("create_at").HasColumnType("datetime");
        builder.Property(x => x.UpdatedAt).HasColumnName("update_at").HasColumnType("datetime");
        builder.HasOne(x => x.User).WithMany(x => x.ChatSessions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Document).WithMany(x => x.ChatSessions).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
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
