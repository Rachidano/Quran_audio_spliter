using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Quran.Infrastructure.Identity;

public static class IdentitySeeder
{
    public const string RoleAdmin = "Admin";
    public const string RoleReviewer = "Reviewer";

    public static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager, ILogger? logger = null)
    {
        string[] roles = [RoleAdmin, RoleReviewer];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole(role, $"Role for {role}"));
                if (result.Succeeded)
                {
                    logger?.LogInformation("Role {Role} created successfully.", role);
                }
                else
                {
                    logger?.LogError("Failed to create role {Role}: {Errors}", role, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    public static async Task<IdentityResult> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string role,
        string? fullName = null,
        ILogger? logger = null)
    {
        var existing = await userManager.FindByEmailAsync(email) ?? await userManager.FindByNameAsync(email);
        if (existing != null)
        {
            logger?.LogInformation("User {Email} already exists. Updating password and ensuring role {Role}.", email, role);
            var token = await userManager.GeneratePasswordResetTokenAsync(existing);
            var resetRes = await userManager.ResetPasswordAsync(existing, token, password);
            if (!resetRes.Succeeded)
            {
                logger?.LogError("Failed to reset password for {Email}: {Errors}", email, string.Join(", ", resetRes.Errors.Select(e => e.Description)));
                return resetRes;
            }

            await userManager.SetLockoutEndDateAsync(existing, null);
            await userManager.ResetAccessFailedCountAsync(existing);

            if (!await userManager.IsInRoleAsync(existing, role))
            {
                await userManager.AddToRoleAsync(existing, role);
                logger?.LogInformation("User {Email} added to role {Role}.", email, role);
            }
            return IdentityResult.Success;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName ?? email
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
            logger?.LogInformation("User {Email} created and assigned role {Role}.", email, role);
        }
        else
        {
            logger?.LogError("Failed to create user {Email}: {Errors}", email, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return result;
    }
}
