using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeightTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBasalMetabolicRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BasalMetabolicRateKcal",
                table: "UserSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "BasalMetabolicRateKcal",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasalMetabolicRateKcal",
                table: "UserSettings");
        }
    }
}
