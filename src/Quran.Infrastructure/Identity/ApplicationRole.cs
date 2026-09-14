using Microsoft.AspNetCore.Identity;

namespace Quran.Infrastructure.Identity;

public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }

    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName) : base(roleName) { }
    public ApplicationRole(string roleName, string description) : base(roleName)
    {
        Description = description;
    }
}
