using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultitenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_uploads_sha256",
                table: "uploads");

            migrationBuilder.DropIndex(
                name: "ix_alerts_rule_id",
                table: "alerts");

            migrationBuilder.DropPrimaryKey(
                name: "pk_audit_events",
                table: "audit_events");

            migrationBuilder.RenameTable(
                name: "audit_events",
                newName: "org_audit_events");

            migrationBuilder.RenameIndex(
                name: "ix_audit_events_event_type_created_at",
                table: "org_audit_events",
                newName: "ix_org_audit_events_event_type_created_at");

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "webhook_sinks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "uploads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "personal_access_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "compare_results",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "analysis_jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "alerts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "org_id",
                table: "org_audit_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_org_audit_events",
                table: "org_audit_events",
                column: "id");

            migrationBuilder.CreateTable(
                name: "orgs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orgs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspace_audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    event_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_memberships",
                columns: table => new
                {
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_memberships", x => new { x.org_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_org_memberships_orgs_org_id",
                        column: x => x.org_id,
                        principalTable: "orgs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_org_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspaces", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspaces_orgs_org_id",
                        column: x => x.org_id,
                        principalTable: "orgs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Seed the default org and workspace with well-known GUIDs.
            // All existing rows (uploads, jobs, alerts, etc.) are then assigned to this workspace.
            migrationBuilder.Sql("""
                INSERT INTO orgs (id, name, slug, created_at)
                VALUES ('00000000-0000-0000-0000-000000000001', 'Default', 'default', NOW())
                ON CONFLICT DO NOTHING;

                INSERT INTO workspaces (id, org_id, name, slug, created_at)
                VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'Default', 'default', NOW())
                ON CONFLICT DO NOTHING;

                UPDATE uploads       SET workspace_id = '00000000-0000-0000-0000-000000000002' WHERE workspace_id = '00000000-0000-0000-0000-000000000000';
                UPDATE analysis_jobs SET workspace_id = '00000000-0000-0000-0000-000000000002' WHERE workspace_id = '00000000-0000-0000-0000-000000000000';
                UPDATE alerts        SET workspace_id = '00000000-0000-0000-0000-000000000002' WHERE workspace_id = '00000000-0000-0000-0000-000000000000';
                UPDATE compare_results SET workspace_id = '00000000-0000-0000-0000-000000000002' WHERE workspace_id = '00000000-0000-0000-0000-000000000000';
                UPDATE webhook_sinks SET workspace_id = '00000000-0000-0000-0000-000000000002' WHERE workspace_id = '00000000-0000-0000-0000-000000000000';
                UPDATE personal_access_tokens SET workspace_id = '00000000-0000-0000-0000-000000000002' WHERE workspace_id = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_uploads_workspace_id_sha256",
                table: "uploads",
                columns: new[] { "workspace_id", "sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alerts_workspace_id_rule_id",
                table: "alerts",
                columns: new[] { "workspace_id", "rule_id" },
                unique: true,
                filter: "status <> 'resolved'");

            migrationBuilder.CreateIndex(
                name: "ix_org_memberships_user_id",
                table: "org_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_orgs_slug",
                table: "orgs",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workspace_audit_events_workspace_id_event_type_created_at",
                table: "workspace_audit_events",
                columns: new[] { "workspace_id", "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_workspaces_org_id_slug",
                table: "workspaces",
                columns: new[] { "org_id", "slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "org_memberships");

            migrationBuilder.DropTable(
                name: "workspace_audit_events");

            migrationBuilder.DropTable(
                name: "workspaces");

            migrationBuilder.DropTable(
                name: "orgs");

            migrationBuilder.DropIndex(
                name: "ix_uploads_workspace_id_sha256",
                table: "uploads");

            migrationBuilder.DropIndex(
                name: "ix_alerts_workspace_id_rule_id",
                table: "alerts");

            migrationBuilder.DropPrimaryKey(
                name: "pk_org_audit_events",
                table: "org_audit_events");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "webhook_sinks");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "uploads");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "personal_access_tokens");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "compare_results");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "analysis_jobs");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "org_id",
                table: "org_audit_events");

            migrationBuilder.RenameTable(
                name: "org_audit_events",
                newName: "audit_events");

            migrationBuilder.RenameIndex(
                name: "ix_org_audit_events_event_type_created_at",
                table: "audit_events",
                newName: "ix_audit_events_event_type_created_at");

            migrationBuilder.AddPrimaryKey(
                name: "pk_audit_events",
                table: "audit_events",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_uploads_sha256",
                table: "uploads",
                column: "sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alerts_rule_id",
                table: "alerts",
                column: "rule_id",
                unique: true,
                filter: "status <> 'resolved'");
        }
    }
}
