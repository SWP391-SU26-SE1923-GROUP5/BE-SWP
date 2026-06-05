using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Data;

public static class SeedData
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        modelBuilder.Entity<User>().HasData(new User
        {
            Id = adminId,
            FullName = "System Administrator",
            Email = "admin@aistudyhub.local",
            PasswordHash = "CHANGE_ME_HASH",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
