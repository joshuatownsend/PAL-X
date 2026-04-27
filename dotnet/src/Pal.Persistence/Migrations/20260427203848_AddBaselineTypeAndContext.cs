using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBaselineTypeAndContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "baseline_context_json",
                table: "analysis_jobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "baseline_type",
                table: "analysis_jobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "baseline_context_json",
                table: "analysis_jobs");

            migrationBuilder.DropColumn(
                name: "baseline_type",
                table: "analysis_jobs");
        }
    }
}
