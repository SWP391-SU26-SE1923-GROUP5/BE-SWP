using Microsoft.AspNetCore.Identity;

namespace AIStudyHub.Data.Entities;

public sealed class User : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public int CurrentStorageCapacity { get; set; }
    public int CurrentAiTokenUsage { get; set; }
    public string Status { get; set; } = "active";
    public string Role { get; set; } = "student";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Guid TierId { get; set; }
    public DateTime? TierExpireAt { get; set; }
    public TierMembership TierMembership { get; set; } = null!;

    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<QuizSubmission> QuizSubmissions { get; set; } = new List<QuizSubmission>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
