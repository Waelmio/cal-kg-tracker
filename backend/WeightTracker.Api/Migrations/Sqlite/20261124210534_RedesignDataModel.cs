using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeightTracker.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class RedesignDataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AveragingWeeks",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "TargetDate",
                table: "Goals",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "Goals",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndedOn",
                table: "Goals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Goals",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Reach");

            migrationBuilder.AddColumn<DateOnly>(
                name: "CaloriesSetOn",
                table: "DailyLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TargetCalories",
                table: "CalorieGoals",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveFrom",
                table: "CalorieGoals",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateTable(
                name: "TdeeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kcal = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TdeeEntries", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "AveragingWeeks",
                value: 2);

            // Data migration (redesign PLAN.md section 4). Keep in step with LegacyUpgrade.cs.
            migrationBuilder.Sql(@"INSERT INTO ""TdeeEntries"" (""Kcal"", ""EffectiveFrom"", ""Source"", ""CreatedAt"")
SELECT ""TdeeKcal"", COALESCE((SELECT MIN(""Date"") FROM ""DailyLogs""), date('now')), 'Manual', strftime('%Y-%m-%dT%H:%M:%S', 'now') || '.0000000+00:00'
FROM ""UserSettings"" WHERE ""Id"" = 1 AND ""TdeeKcal"" IS NOT NULL;");

            migrationBuilder.Sql(@"UPDATE ""CalorieGoals"" SET ""EffectiveFrom"" = substr(""CreatedAt"", 1, 10);");

            migrationBuilder.Sql(@"UPDATE ""DailyLogs"" SET ""CaloriesKcal"" = NULL
WHERE ""CaloriesKcal"" IS NOT NULL
  AND substr(""CreatedAt"", 1, 10) < ""Date""
  AND ""UpdatedAt"" = ""CreatedAt""
  AND ""CaloriesKcal"" = (SELECT g.""TargetCalories"" FROM ""CalorieGoals"" g
                        WHERE g.""EffectiveFrom"" <= ""DailyLogs"".""Date""
                        ORDER BY g.""EffectiveFrom"" DESC, g.""CreatedAt"" DESC LIMIT 1)
  AND EXISTS (SELECT 1 FROM ""DailyLogs"" o WHERE o.""Id"" <> ""DailyLogs"".""Id"" AND o.""CreatedAt"" = ""DailyLogs"".""CreatedAt"");");

            migrationBuilder.Sql(@"DELETE FROM ""DailyLogs""
WHERE ""CaloriesKcal"" IS NULL AND ""WeightKg"" IS NULL AND (""Notes"" IS NULL OR ""Notes"" = '') AND ""IsCheatDay"" = 0;");

            migrationBuilder.Sql(@"UPDATE ""DailyLogs"" SET ""CaloriesSetOn"" = substr(""UpdatedAt"", 1, 10) WHERE ""CaloriesKcal"" IS NOT NULL;");

            migrationBuilder.Sql(@"UPDATE ""Goals""
SET ""EndedOn"" = (SELECT substr(n.""CreatedAt"", 1, 10) FROM ""Goals"" n WHERE n.""CreatedAt"" > ""Goals"".""CreatedAt"" ORDER BY n.""CreatedAt"" ASC LIMIT 1),
    ""EndReason"" = 'Replaced'
WHERE EXISTS (SELECT 1 FROM ""Goals"" n WHERE n.""CreatedAt"" > ""Goals"".""CreatedAt"");");


            migrationBuilder.CreateIndex(
                name: "IX_TdeeEntries_EffectiveFrom",
                table: "TdeeEntries",
                column: "EffectiveFrom");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TdeeEntries");

            migrationBuilder.DropColumn(
                name: "AveragingWeeks",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "EndedOn",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "CaloriesSetOn",
                table: "DailyLogs");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "CalorieGoals");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "TargetDate",
                table: "Goals",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TargetCalories",
                table: "CalorieGoals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
