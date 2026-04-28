using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pal.Persistence.Entities;

namespace Pal.Persistence;

public sealed class PalDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantContext _tenantContext;

    public PalDbContext(DbContextOptions<PalDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<OrgEntity> Orgs => Set<OrgEntity>();
    public DbSet<WorkspaceEntity> Workspaces => Set<WorkspaceEntity>();
    public DbSet<OrgMembershipEntity> OrgMemberships => Set<OrgMembershipEntity>();
    public DbSet<UploadEntity> Uploads => Set<UploadEntity>();
    public DbSet<AnalysisJobEntity> AnalysisJobs => Set<AnalysisJobEntity>();
    public DbSet<AnalysisJobPackEntity> AnalysisJobPacks => Set<AnalysisJobPackEntity>();
    public DbSet<AnalysisResultEntity> AnalysisResults => Set<AnalysisResultEntity>();
    public DbSet<AnalysisReportEntity> AnalysisReports => Set<AnalysisReportEntity>();
    public DbSet<PackEntity> Packs => Set<PackEntity>();
    public DbSet<PackVersionEntity> PackVersions => Set<PackVersionEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    public DbSet<WorkspaceAuditEventEntity> WorkspaceAuditEvents => Set<WorkspaceAuditEventEntity>();
    public DbSet<CompareResultEntity> CompareResults => Set<CompareResultEntity>();
    public DbSet<AlertEntity> Alerts => Set<AlertEntity>();
    public DbSet<WebhookSinkEntity> WebhookSinks => Set<WebhookSinkEntity>();
    public DbSet<IngestionScheduleEntity> IngestionSchedules => Set<IngestionScheduleEntity>();
    public DbSet<PersonalAccessTokenEntity> PersonalAccessTokens => Set<PersonalAccessTokenEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);  // must be first — registers Identity table configs

        // Remap Identity tables to snake_case; UseSnakeCaseNamingConvention does not
        // override the explicit ToTable() calls that IdentityDbContext.OnModelCreating sets.
        modelBuilder.Entity<ApplicationUser>().ToTable("asp_net_users");
        modelBuilder.Entity<IdentityRole>().ToTable("asp_net_roles");
        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("asp_net_user_claims");
        modelBuilder.Entity<IdentityUserRole<string>>().ToTable("asp_net_user_roles");
        modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("asp_net_user_logins");
        modelBuilder.Entity<IdentityUserToken<string>>().ToTable("asp_net_user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("asp_net_role_claims");

        // Rename audit_events → org_audit_events
        modelBuilder.Entity<AuditEventEntity>(e =>
        {
            e.ToTable("org_audit_events");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventType, x.CreatedAt });
        });

        modelBuilder.Entity<OrgEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<WorkspaceEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrgId, x.Slug }).IsUnique();
            e.HasOne(x => x.Org)
                .WithMany(x => x.Workspaces)
                .HasForeignKey(x => x.OrgId);
        });

        modelBuilder.Entity<OrgMembershipEntity>(e =>
        {
            e.HasKey(x => new { x.OrgId, x.UserId });
            e.HasOne(x => x.Org)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.OrgId);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkspaceAuditEventEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.WorkspaceId, x.EventType, x.CreatedAt });
        });

        // Workspace-scoped entities: filter passes all rows when WorkspaceId is null (system/worker scope).
        modelBuilder.Entity<UploadEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.WorkspaceId, x.Sha256 }).IsUnique();
            e.HasQueryFilter(u => !_tenantContext.WorkspaceId.HasValue
                               || u.WorkspaceId == _tenantContext.WorkspaceId.GetValueOrDefault());
        });

        modelBuilder.Entity<AnalysisJobEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.UploadId);
            e.HasOne(x => x.Upload)
                .WithMany(x => x.AnalysisJobs)
                .HasForeignKey(x => x.UploadId);
            e.HasQueryFilter(j => !_tenantContext.WorkspaceId.HasValue
                               || j.WorkspaceId == _tenantContext.WorkspaceId.GetValueOrDefault());
        });

        modelBuilder.Entity<AnalysisJobPackEntity>(e =>
        {
            e.HasKey(x => new { x.AnalysisJobId, x.PackId });
            e.HasOne(x => x.AnalysisJob)
                .WithMany(x => x.Packs)
                .HasForeignKey(x => x.AnalysisJobId);
        });

        modelBuilder.Entity<AnalysisResultEntity>(e =>
        {
            e.HasKey(x => x.AnalysisJobId);
            e.HasOne(x => x.AnalysisJob)
                .WithOne(x => x.Result)
                .HasForeignKey<AnalysisResultEntity>(x => x.AnalysisJobId);
        });

        modelBuilder.Entity<AnalysisReportEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.AnalysisJob)
                .WithMany(x => x.Reports)
                .HasForeignKey(x => x.AnalysisJobId);
        });

        modelBuilder.Entity<PackEntity>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<PackVersionEntity>(e =>
        {
            e.HasKey(x => new { x.PackId, x.Version });
            e.HasOne(x => x.Pack)
                .WithMany(x => x.Versions)
                .HasForeignKey(x => x.PackId);
        });

        modelBuilder.Entity<CompareResultEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.BaselineJob)
                .WithMany()
                .HasForeignKey(x => x.BaselineJobId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CandidateJob)
                .WithMany()
                .HasForeignKey(x => x.CandidateJobId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(c => !_tenantContext.WorkspaceId.HasValue
                               || c.WorkspaceId == _tenantContext.WorkspaceId.GetValueOrDefault());
        });

        modelBuilder.Entity<AlertEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Status, x.LastSeenAt });
            e.HasIndex(x => new { x.WorkspaceId, x.RuleId })
                .IsUnique()
                .HasFilter("status <> 'resolved'");
            e.HasQueryFilter(a => !_tenantContext.WorkspaceId.HasValue
                               || a.WorkspaceId == _tenantContext.WorkspaceId.GetValueOrDefault());
        });

        modelBuilder.Entity<WebhookSinkEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasQueryFilter(w => !_tenantContext.WorkspaceId.HasValue
                               || w.WorkspaceId == _tenantContext.WorkspaceId.GetValueOrDefault());
        });

        modelBuilder.Entity<IngestionScheduleEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.WorkspaceId, x.Name }).IsUnique();
            e.HasIndex(x => new { x.Enabled, x.NextRunAt });
            e.HasQueryFilter(s => !_tenantContext.WorkspaceId.HasValue
                               || s.WorkspaceId == _tenantContext.WorkspaceId.GetValueOrDefault());
        });

        modelBuilder.Entity<PersonalAccessTokenEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(t => !_tenantContext.WorkspaceId.HasValue
                               || t.WorkspaceId == _tenantContext.WorkspaceId.GetValueOrDefault());
        });
    }
}
