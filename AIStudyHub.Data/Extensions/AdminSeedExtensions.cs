using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIStudyHub.Data.Extensions;

public static class AdminSeedExtensions
{
    public static async Task SeedConfiguredAdminAsync(this IServiceProvider services, IConfiguration configuration)
    {
        var adminSection = configuration.GetSection("AdminSeed");
        var enabled = !bool.TryParse(adminSection["Enabled"], out var parsedEnabled) || parsedEnabled;
        var options = new AdminSeedOptions
        {
            Enabled = enabled,
            FullName = adminSection["FullName"] ?? string.Empty,
            Email = adminSection["Email"] ?? string.Empty,
            Password = adminSection["Password"] ?? string.Empty
        };

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            return;
        }

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        const string adminRole = "Admin";

        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(adminRole));
        }

        var normalizedEmail = options.Email.Trim().ToLowerInvariant();
        var existingAdmin = await userManager.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(user => new { user.Id, user.Email })
            .SingleOrDefaultAsync(user => user.Email == normalizedEmail);

        if (existingAdmin is not null)
        {
            return;
        }

        var admin = new User
        {
            Id = Guid.NewGuid(),
            FullName = string.IsNullOrWhiteSpace(options.FullName) ? "System Administrator" : options.FullName.Trim(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true,
            CurrentStorageCapacity = 0,
            CurrentAiTokenUsage = 0,
            Status = "active",
            Role = "admin",
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(admin, options.Password);

        if (!createResult.Succeeded)
        {
            var errorMessages = createResult.Errors.Select(error =>
            {
                return error.Code switch
                {
                    "PasswordTooShort" => $"Password must be at least {options.Password.Length} characters (minimum: 12)",
                    "PasswordRequiresDigit" => "Password must contain at least one digit (0-9)",
                    "PasswordRequiresUpper" => "Password must contain at least one uppercase letter (A-Z)",
                    "PasswordRequiresLower" => "Password must contain at least one lowercase letter (a-z)",
                    "PasswordRequiresNonAlphanumeric" => "Password must contain at least one special character (!@#$%^&*)",
                    "PasswordRequiresUniqueChars" => $"Password must have at least {6} unique characters",
                    _ => error.Description
                };
            });
            throw new InvalidOperationException($"Failed to seed admin account: {string.Join("; ", errorMessages)}");
        }

        var roleResult = await userManager.AddToRoleAsync(admin, adminRole);

        if (!roleResult.Succeeded)
        {
            var errors = string.Join("; ", roleResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to assign admin role: {errors}");
        }
    }
}
