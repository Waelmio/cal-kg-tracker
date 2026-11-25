using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WeightTracker.Api.Migrations.Postgres
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
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "TargetDate",
                table: "Goals",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "Goals",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndedOn",
                table: "Goals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Goals",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Reach");

            migrationBuilder.AddColumn<DateOnly>(
                name: "CaloriesSetOn",
                table: "DailyLogs",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TargetCalories",
                table: "CalorieGoals",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveFrom",
                table: "CalorieGoals",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateTable(
                name: "TdeeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kcal = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            // Data migration (redesign PLAN.md section 4). No stored value is cleared or deleted: auto-filled days are indistinguishable from typed values, so they are kept.
            migrationBuilder.Sql(@"INSERT INTO ""TdeeEntries"" (""Kcal"", ""EffectiveFrom"", ""Source"", ""CreatedAt"")
SELECT ""TdeeKcal"", COALESCE((SELECT MIN(""Date"") FROM ""DailyLogs""), CURRENT_DATE), 'Manual', now()
FROM ""UserSettings"" WHERE ""Id"" = 1 AND ""TdeeKcal"" IS NOT NULL;");

            migrationBuilder.Sql(@"UPDATE ""CalorieGoals"" SET ""EffectiveFrom"" = (""CreatedAt"" AT TIME ZONE 'UTC')::date;");

            migrationBuilder.Sql(@"UPDATE ""DailyLogs"" SET ""CaloriesSetOn"" = (""UpdatedAt"" AT TIME ZONE 'UTC')::date WHERE ""CaloriesKcal"" IS NOT NULL;");

            migrationBuilder.Sql(@"UPDATE ""Goals"" g
SET ""EndedOn"" = (SELECT (n.""CreatedAt"" AT TIME ZONE 'UTC')::date FROM ""Goals"" n WHERE n.""CreatedAt"" > g.""CreatedAt"" ORDER BY n.""CreatedAt"" ASC LIMIT 1),
    ""EndReason"" = 'Replaced'
WHERE EXISTS (SELECT 1 FROM ""Goals"" n WHERE n.""CreatedAt"" > g.""CreatedAt"");");


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
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TargetCalories",
                table: "CalorieGoals",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
