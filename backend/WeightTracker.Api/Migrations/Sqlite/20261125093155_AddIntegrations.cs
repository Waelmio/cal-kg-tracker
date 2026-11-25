using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeightTracker.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaloriesSource",
                table: "DailyLogs",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeightSource",
                table: "DailyLogs",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntegrationConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTokenSecret = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ConnectedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastSyncAt = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationConnections_Provider",
                table: "IntegrationConnections",
                column: "Provider",
                unique: true);

            // Everything stored so far was the user's: an import must not replace it.
            migrationBuilder.Sql(@"UPDATE ""DailyLogs"" SET ""CaloriesSource"" = 'User' WHERE ""CaloriesKcal"" IS NOT NULL;");
            migrationBuilder.Sql(@"UPDATE ""DailyLogs"" SET ""WeightSource"" = 'User' WHERE ""WeightKg"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationConnections");

            migrationBuilder.DropColumn(
                name: "CaloriesSource",
                table: "DailyLogs");

            migrationBuilder.DropColumn(
                name: "WeightSource",
                table: "DailyLogs");
        }
    }
}
