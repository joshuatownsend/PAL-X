using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUploadSha256 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_uploads_sha256",
                table: "uploads");

            migrationBuilder.CreateIndex(
                name: "ix_uploads_sha256",
                table: "uploads",
                column: "sha256",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_uploads_sha256",
                table: "uploads");

            migrationBuilder.CreateIndex(
                name: "ix_uploads_sha256",
                table: "uploads",
                column: "sha256");
        }
    }
}
