using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeightTracker.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class DropLegacyTdee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The value has lived in TdeeEntries since RedesignDataModel. Should a database have one without the
            // other, keep it: nothing stored is lost by an upgrade.
            migrationBuilder.Sql(@"INSERT INTO ""TdeeEntries"" (""Kcal"", ""EffectiveFrom"", ""Source"", ""CreatedAt"")
SELECT ""TdeeKcal"", COALESCE((SELECT MIN(""Date"") FROM ""DailyLogs""), CURRENT_DATE), 'Manual', now()
FROM ""UserSettings"" WHERE ""Id"" = 1 AND ""TdeeKcal"" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ""TdeeEntries"");");

            migrationBuilder.DropColumn(
                name: "TdeeKcal",
                table: "UserSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TdeeKcal",
                table: "UserSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"UPDATE ""UserSettings"" SET ""TdeeKcal"" = (SELECT ""Kcal"" FROM ""TdeeEntries"" ORDER BY ""EffectiveFrom"" DESC, ""Id"" DESC LIMIT 1) WHERE ""Id"" = 1;");
        }
    }
}
