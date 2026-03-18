using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeightTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameBasalMetabolicRateToTdee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BasalMetabolicRateKcal",
                table: "UserSettings",
                newName: "TdeeKcal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TdeeKcal",
                table: "UserSettings",
                newName: "BasalMetabolicRateKcal");
        }
    }
}
