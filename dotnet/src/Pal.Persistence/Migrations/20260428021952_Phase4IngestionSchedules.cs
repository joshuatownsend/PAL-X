using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4IngestionSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingestion_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    source_config_json = table.Column<string>(type: "text", nullable: false),
                    pack_ids = table.Column<string>(type: "text", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingestion_schedules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_schedules_enabled_next_run_at",
                table: "ingestion_schedules",
                columns: new[] { "enabled", "next_run_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_schedules_workspace_id_name",
                table: "ingestion_schedules",
                columns: new[] { "workspace_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingestion_schedules");
        }
    }
}
