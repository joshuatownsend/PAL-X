using Microsoft.AspNetCore.Identity;

namespace Pal.Persistence.Entities;

public sealed class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
