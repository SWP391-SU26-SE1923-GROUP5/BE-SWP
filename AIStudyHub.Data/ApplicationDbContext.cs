using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Data;

public sealed class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<TierMembership> TierMemberships => Set<TierMembership>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<FlashcardReview> FlashcardReviews => Set<FlashcardReview>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<QuizSubmission> QuizSubmissions => Set<QuizSubmission>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpRecord> OtpRecords => Set<OtpRecord>();
    public DbSet<UserStats> UserStats => Set<UserStats>();
    public DbSet<StudyLog> StudyLogs => Set<StudyLog>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<DocumentShare> DocumentShares => Set<DocumentShare>();
    public DbSet<TokenLedger> TokenLedgers => Set<TokenLedger>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        SeedRoles(modelBuilder);
        SeedTiers(modelBuilder);
        SeedBadges(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    private void ApplyAuditFields()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        var userEntries = ChangeTracker.Entries<User>();

        foreach (var entry in userEntries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityRole<Guid>>().HasData(
            CreateRole(Guid.Parse("22222222-2222-2222-2222-222222222222"), UserRole.Student.ToString()),
            CreateRole(Guid.Parse("44444444-4444-4444-4444-444444444444"), UserRole.Admin.ToString()));
    }

    private static IdentityRole<Guid> CreateRole(Guid id, string name)
    {
        return new IdentityRole<Guid>
        {
            Id = id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ConcurrencyStamp = id.ToString()
        };
    }

    private static void SeedTiers(ModelBuilder modelBuilder)
    {
        var seedTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<TierMembership>().HasData(
            new TierMembership
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                TierName = "Free",
                Price = 0m,
                StorageLimitMb = 1024,
                AiTokens = 1000000,
                CreatedAt = seedTimestamp
            },
            new TierMembership
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                TierName = "Pro",
                Price = 499000m,
                StorageLimitMb = 5120,
                AiTokens = 5000000,
                CreatedAt = seedTimestamp
            },
            new TierMembership
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                TierName = "Premium",
                Price = 199000m,
                StorageLimitMb = 3072,
                AiTokens = 3000000,
                CreatedAt = seedTimestamp
            });
    }

    /// <summary>
    /// Seeds the 5 Master Spec badges. Ids are deterministic so migrations stay repeatable.
    /// </summary>
    private static void SeedBadges(ModelBuilder modelBuilder)
    {
        var seedTimestamp = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Badge>().HasData(
            new Badge
            {
                Id = Guid.Parse("aaaaaaaa-0001-0000-0000-000000000001"),
                Code = "STREAK_7D",
                Title = "7-Day Streak",
                Description = "Maintain a 7-day study streak.",
                Category = "Streak",
                TargetValue = 7m,
                IconUrl = "/badges/streak-7d.svg",
                XpReward = 100,
                CreatedAt = seedTimestamp
            },
            new Badge
            {
                Id = Guid.Parse("aaaaaaaa-0002-0000-0000-000000000002"),
                Code = "CARDS_500",
                Title = "Memory Master",
                Description = "Review 500 flashcards.",
                Category = "Volume",
                TargetValue = 500m,
                IconUrl = "/badges/memory-master.svg",
                XpReward = 150,
                CreatedAt = seedTimestamp
            },
            new Badge
            {
                Id = Guid.Parse("aaaaaaaa-0003-0000-0000-000000000003"),
                Code = "MASTERY_MATH",
                Title = "Math Prodigy",
                Description = "Reach 85% or higher in Mathematics.",
                Category = "Mastery",
                TargetValue = 85m,
                IconUrl = "/badges/math-prodigy.svg",
                XpReward = 120,
                CreatedAt = seedTimestamp
            },
            new Badge
            {
                Id = Guid.Parse("aaaaaaaa-0004-0000-0000-000000000004"),
                Code = "SHARPSHOOTER",
                Title = "Sharpshooter",
                Description = "Score 100% on a quiz with at least 10 questions on the first attempt.",
                Category = "Accuracy",
                TargetValue = 100m,
                IconUrl = "/badges/sharpshooter.svg",
                XpReward = 200,
                CreatedAt = seedTimestamp
            },
            new Badge
            {
                Id = Guid.Parse("aaaaaaaa-0005-0000-0000-000000000005"),
                Code = "BOOKWORM",
                Title = "Bookworm",
                Description = "Successfully process 7 documents.",
                Category = "Content",
                TargetValue = 7m,
                IconUrl = "/badges/bookworm.svg",
                XpReward = 80,
                CreatedAt = seedTimestamp
            });
    }
}
