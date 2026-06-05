using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Options;
using Microsoft.AspNetCore.Identity;
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
        var adminRole = UserRole.Admin.ToString();

        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(adminRole));
        }

        var normalizedEmail = options.Email.Trim().ToLowerInvariant();
        var existingAdmin = await userManager.FindByEmailAsync(normalizedEmail);

        if (existingAdmin is not null)
        {
            if (!await userManager.IsInRoleAsync(existingAdmin, adminRole))
            {
                await userManager.AddToRoleAsync(existingAdmin, adminRole);
            }

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
            CurrentAiToken = 0,
            Status = "Active",
            Role = UserRole.Admin,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(admin, options.Password);

        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to seed admin account: {errors}");
        }

        var roleResult = await userManager.AddToRoleAsync(admin, adminRole);

        if (!roleResult.Succeeded)
        {
            var errors = string.Join("; ", roleResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to assign admin role: {errors}");
        }
    }
}
