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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        SeedRoles(modelBuilder);
        SeedTiers(modelBuilder);
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
        modelBuilder.Entity<TierMembership>().HasData(
            new TierMembership
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                TierName = "Free",
                StorageLimitMb = 1024,
                AiTokens = 10000,
                CreatedAt = DateTime.UtcNow
            },
            new TierMembership
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                TierName = "Premium",
                StorageLimitMb = 3072,
                AiTokens = 30000,
                CreatedAt = DateTime.UtcNow
            });
    }
}
