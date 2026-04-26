using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4AlertingUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_alerts_rule_id",
                table: "alerts");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_rule_id",
                table: "alerts",
                column: "rule_id",
                unique: true,
                filter: "status <> 'resolved'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_alerts_rule_id",
                table: "alerts");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_rule_id",
                table: "alerts",
                column: "rule_id");
        }
    }
}
