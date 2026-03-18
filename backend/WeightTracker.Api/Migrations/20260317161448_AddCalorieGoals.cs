using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeightTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCalorieGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyCalorieTarget",
                table: "UserSettings");

            migrationBuilder.CreateTable(
                name: "CalorieGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetCalories = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalorieGoals", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalorieGoals");

            migrationBuilder.AddColumn<int>(
                name: "DailyCalorieTarget",
                table: "UserSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "DailyCalorieTarget",
                value: null);
        }
    }
}
