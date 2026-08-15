using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsulinAndCoffee.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMealCarbAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CarbAdjustment",
                table: "Meals",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CarbOverride",
                table: "MealItems",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarbAdjustment",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "CarbOverride",
                table: "MealItems");
        }
    }
}
