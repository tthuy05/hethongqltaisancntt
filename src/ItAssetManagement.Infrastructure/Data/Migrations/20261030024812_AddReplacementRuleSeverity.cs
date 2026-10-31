using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItAssetManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReplacementRuleSeverity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "severity",
                schema: "public",
                table: "replacement_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_replacement_rules_severity",
                schema: "public",
                table: "replacement_rules",
                sql: "severity IS NULL OR severity IN ('LOW','MEDIUM','HIGH','CRITICAL')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_replacement_rules_severity",
                schema: "public",
                table: "replacement_rules");

            migrationBuilder.DropColumn(
                name: "severity",
                schema: "public",
                table: "replacement_rules");
        }
    }
}
