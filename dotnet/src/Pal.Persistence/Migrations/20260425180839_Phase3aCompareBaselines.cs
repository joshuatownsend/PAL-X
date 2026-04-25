using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3aCompareBaselines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "baseline_label",
                table: "analysis_jobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_baseline",
                table: "analysis_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "compare_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    baseline_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_compare_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_compare_results_analysis_jobs_baseline_job_id",
                        column: x => x.baseline_job_id,
                        principalTable: "analysis_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_compare_results_analysis_jobs_candidate_job_id",
                        column: x => x.candidate_job_id,
                        principalTable: "analysis_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_compare_results_baseline_job_id",
                table: "compare_results",
                column: "baseline_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_compare_results_candidate_job_id",
                table: "compare_results",
                column: "candidate_job_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compare_results");

            migrationBuilder.DropColumn(
                name: "baseline_label",
                table: "analysis_jobs");

            migrationBuilder.DropColumn(
                name: "is_baseline",
                table: "analysis_jobs");
        }
    }
}
