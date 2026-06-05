using AIStudyHub.Data.Enums;
using Microsoft.AspNetCore.Identity;

namespace AIStudyHub.Data.Entities;

public sealed class User : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public Guid? TierId { get; set; }
    public int CurrentStorageCapacity { get; set; }
    public int CurrentAiToken { get; set; }
    public string Status { get; set; } = "Active";
    public UserRole Role { get; set; } = UserRole.Student;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<QuizSubmission> QuizSubmissions { get; set; } = new List<QuizSubmission>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
}
