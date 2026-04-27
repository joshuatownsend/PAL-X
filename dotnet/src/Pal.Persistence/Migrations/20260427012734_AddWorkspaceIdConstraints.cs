using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceIdConstraints : Migration
    {
        private static readonly string[] WorkspaceScopedTables =
        [
            "uploads", "analysis_jobs", "compare_results",
            "webhook_sinks", "personal_access_tokens", "alerts",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the Guid.Empty column defaults left by the backfill migration.
            // After backfill every row has a real workspace_id; leaving the default would let
            // raw/tooling inserts silently land in a phantom workspace.
            foreach (var table in WorkspaceScopedTables)
                migrationBuilder.Sql($"ALTER TABLE {table} ALTER COLUMN workspace_id DROP DEFAULT;");

            // FK: workspace-scoped data tables → workspaces.id (Cascade: deleting a workspace
            // purges its data; the retention job handles graceful cleanup before that point).
            foreach (var table in WorkspaceScopedTables)
            {
                migrationBuilder.AddForeignKey(
                    name: $"fk_{table}_workspaces_workspace_id",
                    table: table,
                    column: "workspace_id",
                    principalTable: "workspaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            }

            // workspace_audit_events was created in the previous migration without a FK.
            migrationBuilder.AddForeignKey(
                name: "fk_workspace_audit_events_workspaces_workspace_id",
                table: "workspace_audit_events",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // org_audit_events.org_id is nullable — use Restrict so audit rows outlive the org
            // rather than silently disappearing (callers should clean up audit rows first).
            migrationBuilder.AddForeignKey(
                name: "fk_org_audit_events_orgs_org_id",
                table: "org_audit_events",
                column: "org_id",
                principalTable: "orgs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("fk_org_audit_events_orgs_org_id", "org_audit_events");
            migrationBuilder.DropForeignKey("fk_workspace_audit_events_workspaces_workspace_id", "workspace_audit_events");

            foreach (var table in WorkspaceScopedTables)
                migrationBuilder.DropForeignKey($"fk_{table}_workspaces_workspace_id", table);

            // Restore the Guid.Empty defaults so the prior migration's Up is still replayable.
            foreach (var table in WorkspaceScopedTables)
                migrationBuilder.Sql(
                    $"ALTER TABLE {table} ALTER COLUMN workspace_id SET DEFAULT '00000000-0000-0000-0000-000000000000';");
        }
    }
}
