namespace Pal.Persistence;

/// <summary>
/// Well-known GUIDs seeded by the AddMultitenancy migration. Referencing these constants
/// (rather than hard-coding strings) avoids typos across migration SQL and application code.
/// </summary>
public static class DefaultTenant
{
    public static readonly Guid OrgId = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid WorkspaceId = new("00000000-0000-0000-0000-000000000002");
}
