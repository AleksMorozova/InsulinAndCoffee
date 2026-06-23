using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InsulinAndCoffee.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedKnownMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "KnownMeals",
                columns: new[] { "Id", "Carbs", "CreatedAt", "DishName", "IsFavorite", "LastPreMealGlucose", "LastUsedAt", "Notes", "PlaceName", "PortionDescription", "ResultRating", "Tags", "UsageCount", "UserId", "UsualInsulinUnits" },
                values: new object[,]
                {
                    { new Guid("44444444-4444-4444-4444-444444444401"), 95m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Philadelphia Set", true, null, null, "Reliable repeat order.", "Sushi Master", "standard set", 2, "sushi, delivery, dinner", 0, new Guid("11111111-1111-1111-1111-111111111111"), 7m },
                    { new Guid("44444444-4444-4444-4444-444444444402"), 67m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cottage Cheese Casserole", true, null, null, "Good with coffee.", "Local Cafe", "one slice", 2, "cafe, dessert, breakfast", 0, new Guid("11111111-1111-1111-1111-111111111111"), 6m },
                    { new Guid("44444444-4444-4444-4444-444444444403"), 80m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Shawarma", false, null, null, "Check glucose response next time.", "Delivery", "standard", 5, "delivery, lunch", 0, new Guid("11111111-1111-1111-1111-111111111111"), 8m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "KnownMeals",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444401"));

            migrationBuilder.DeleteData(
                table: "KnownMeals",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444402"));

            migrationBuilder.DeleteData(
                table: "KnownMeals",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444403"));
        }
    }
}
