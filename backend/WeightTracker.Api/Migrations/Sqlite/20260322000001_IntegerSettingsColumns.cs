using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeightTracker.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class IntegerSettingsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite does not support ALTER COLUMN, so we rebuild the table.
            migrationBuilder.Sql("""
                CREATE TABLE "UserSettings_new" (
                    "Id"            INTEGER NOT NULL CONSTRAINT "PK_UserSettings" PRIMARY KEY AUTOINCREMENT,
                    "HeightCm"      INTEGER NULL,
                    "PreferredUnit" TEXT    NOT NULL,
                    "TdeeKcal"      INTEGER NULL
                );
                INSERT INTO "UserSettings_new" ("Id", "HeightCm", "PreferredUnit", "TdeeKcal")
                SELECT "Id",
                       CAST(ROUND("HeightCm") AS INTEGER),
                       "PreferredUnit",
                       CAST(ROUND("TdeeKcal") AS INTEGER)
                FROM "UserSettings";
                DROP TABLE "UserSettings";
                ALTER TABLE "UserSettings_new" RENAME TO "UserSettings";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE "UserSettings_old" (
                    "Id"            INTEGER NOT NULL CONSTRAINT "PK_UserSettings" PRIMARY KEY AUTOINCREMENT,
                    "HeightCm"      TEXT NULL,
                    "PreferredUnit" TEXT NOT NULL,
                    "TdeeKcal"      TEXT NULL
                );
                INSERT INTO "UserSettings_old" ("Id", "HeightCm", "PreferredUnit", "TdeeKcal")
                SELECT "Id", "HeightCm", "PreferredUnit", "TdeeKcal" FROM "UserSettings";
                DROP TABLE "UserSettings";
                ALTER TABLE "UserSettings_old" RENAME TO "UserSettings";
                """);
        }
    }
}
