using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDatasetArtifact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "dataset_byte_length",
                table: "analysis_results",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "dataset_compressed",
                table: "analysis_results",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dataset_storage_path",
                table: "analysis_results",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dataset_byte_length",
                table: "analysis_results");

            migrationBuilder.DropColumn(
                name: "dataset_compressed",
                table: "analysis_results");

            migrationBuilder.DropColumn(
                name: "dataset_storage_path",
                table: "analysis_results");
        }
    }
}
