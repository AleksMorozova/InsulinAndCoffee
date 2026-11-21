using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsulinAndCoffee.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCarbProvenanceAndVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CarbRevision",
                table: "Meals",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CarbsVerifiedAt",
                table: "Meals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CarbReferenceSource",
                table: "MealItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CarbTransformation",
                table: "MealItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SourceMealVerifiedAt",
                table: "MealItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CarbReferenceSource",
                table: "FoodItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CarbReferenceSource",
                table: "DeliveryMeals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CarbTransformation",
                table: "DeliveryMeals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SourceMealVerifiedAt",
                table: "DeliveryMeals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "DeliveryMeals",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444401"),
                columns: new[] { "CarbReferenceSource", "CarbTransformation", "SourceMealVerifiedAt" },
                values: new object[] { 0, 0, null });

            migrationBuilder.UpdateData(
                table: "DeliveryMeals",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444402"),
                columns: new[] { "CarbReferenceSource", "CarbTransformation", "SourceMealVerifiedAt" },
                values: new object[] { 0, 0, null });

            migrationBuilder.UpdateData(
                table: "DeliveryMeals",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444403"),
                columns: new[] { "CarbReferenceSource", "CarbTransformation", "SourceMealVerifiedAt" },
                values: new object[] { 0, 0, null });

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333301"),
                column: "CarbReferenceSource",
                value: 0);

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333302"),
                column: "CarbReferenceSource",
                value: 0);

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333303"),
                column: "CarbReferenceSource",
                value: 0);

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333304"),
                column: "CarbReferenceSource",
                value: 0);

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333305"),
                column: "CarbReferenceSource",
                value: 0);

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333306"),
                column: "CarbReferenceSource",
                value: 0);

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333307"),
                column: "CarbReferenceSource",
                value: 0);

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333308"),
                column: "CarbReferenceSource",
                value: 0);

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333309"),
                column: "CarbReferenceSource",
                value: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarbRevision",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "CarbsVerifiedAt",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "CarbReferenceSource",
                table: "MealItems");

            migrationBuilder.DropColumn(
                name: "CarbTransformation",
                table: "MealItems");

            migrationBuilder.DropColumn(
                name: "SourceMealVerifiedAt",
                table: "MealItems");

            migrationBuilder.DropColumn(
                name: "CarbReferenceSource",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "CarbReferenceSource",
                table: "DeliveryMeals");

            migrationBuilder.DropColumn(
                name: "CarbTransformation",
                table: "DeliveryMeals");

            migrationBuilder.DropColumn(
                name: "SourceMealVerifiedAt",
                table: "DeliveryMeals");
        }
    }
}
