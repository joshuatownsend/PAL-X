using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pal.Persistence.Entities;

namespace Pal.Persistence;

public sealed class PalDbContext : IdentityDbContext<ApplicationUser>
{
    public PalDbContext(DbContextOptions<PalDbContext> options) : base(options) { }

    public DbSet<UploadEntity> Uploads => Set<UploadEntity>();
    public DbSet<AnalysisJobEntity> AnalysisJobs => Set<AnalysisJobEntity>();
    public DbSet<AnalysisJobPackEntity> AnalysisJobPacks => Set<AnalysisJobPackEntity>();
    public DbSet<AnalysisResultEntity> AnalysisResults => Set<AnalysisResultEntity>();
    public DbSet<AnalysisReportEntity> AnalysisReports => Set<AnalysisReportEntity>();
    public DbSet<PackEntity> Packs => Set<PackEntity>();
    public DbSet<PackVersionEntity> PackVersions => Set<PackVersionEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    public DbSet<CompareResultEntity> CompareResults => Set<CompareResultEntity>();
    public DbSet<AlertEntity> Alerts => Set<AlertEntity>();
    public DbSet<WebhookSinkEntity> WebhookSinks => Set<WebhookSinkEntity>();
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

        modelBuilder.Entity<UploadEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Sha256).IsUnique();
        });

        modelBuilder.Entity<AnalysisJobEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.UploadId);
            e.HasOne(x => x.Upload)
                .WithMany(x => x.AnalysisJobs)
                .HasForeignKey(x => x.UploadId);
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

        modelBuilder.Entity<AuditEventEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventType, x.CreatedAt });
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
        });

        modelBuilder.Entity<AlertEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Status, x.LastSeenAt });
            // Prevents concurrent job evaluations from creating duplicate active alerts for the same rule.
            e.HasIndex(x => x.RuleId)
                .IsUnique()
                .HasFilter("status <> 'resolved'");
        });

        modelBuilder.Entity<WebhookSinkEntity>(e =>
        {
            e.HasKey(x => x.Id);
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
        });
    }
}
