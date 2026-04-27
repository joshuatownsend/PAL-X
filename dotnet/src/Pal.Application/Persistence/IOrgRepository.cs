namespace Pal.Application.Persistence;

public sealed class OrgDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class WorkspaceDto
{
    public required Guid Id { get; init; }
    public required Guid OrgId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class OrgMembershipDto
{
    public required Guid OrgId { get; init; }
    public required string UserId { get; init; }
    public required string Role { get; init; }
    public required DateTimeOffset JoinedAt { get; init; }
}

public interface IOrgRepository
{
    Task<IReadOnlyList<OrgDto>> ListAsync(CancellationToken ct = default);
    Task<OrgDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<OrgDto> CreateAsync(string name, string slug, CancellationToken ct = default);
    Task<IReadOnlyList<WorkspaceDto>> ListWorkspacesAsync(Guid orgId, CancellationToken ct = default);
    Task<WorkspaceDto?> GetWorkspaceAsync(Guid workspaceId, CancellationToken ct = default);
    Task<WorkspaceDto> CreateWorkspaceAsync(Guid orgId, string name, string slug, CancellationToken ct = default);
    Task<IReadOnlyList<OrgMembershipDto>> ListMembersAsync(Guid orgId, CancellationToken ct = default);
    Task<OrgMembershipDto?> GetMembershipAsync(Guid orgId, string userId, CancellationToken ct = default);
    Task UpsertMembershipAsync(Guid orgId, string userId, string role, CancellationToken ct = default);
    Task<bool> RemoveMembershipAsync(Guid orgId, string userId, CancellationToken ct = default);
}
