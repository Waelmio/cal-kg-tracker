using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeightTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Goals_CreatedAt",
                table: "Goals",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CalorieGoals_CreatedAt",
                table: "CalorieGoals",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Goals_CreatedAt",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_CalorieGoals_CreatedAt",
                table: "CalorieGoals");
        }
    }
}
