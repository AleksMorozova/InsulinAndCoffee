using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsulinAndCoffee.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodMeasurementTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:food_measurement_type", "grams,portion,piece")
                .Annotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .Annotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual")
                .Annotation("Npgsql:Enum:result_rating", "perfect,good,high_glucose,low_glucose,unknown")
                .OldAnnotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .OldAnnotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual")
                .OldAnnotation("Npgsql:Enum:result_rating", "perfect,good,high_glucose,low_glucose,unknown");

            migrationBuilder.AlterColumn<decimal>(
                name: "WeightGrams",
                table: "MealItems",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,2)",
                oldPrecision: 8,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "CarbsPer100gSnapshot",
                table: "MealItems",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(7,2)",
                oldPrecision: 7,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "CarbsPerUnitSnapshot",
                table: "MealItems",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeasurementType",
                table: "MealItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "MealItems",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE "MealItems"
                SET "Quantity" = "WeightGrams",
                    "MeasurementType" = 0
                WHERE "Quantity" = 0;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "CarbsPer100g",
                table: "FoodItems",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(7,2)",
                oldPrecision: 7,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "CarbsPerUnit",
                table: "FoodItems",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeasurementType",
                table: "FoodItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333301"),
                columns: new[] { "CarbsPerUnit", "MeasurementType" },
                values: new object[] { null, 0 });

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333302"),
                columns: new[] { "CarbsPerUnit", "MeasurementType" },
                values: new object[] { null, 0 });

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333303"),
                columns: new[] { "CarbsPerUnit", "MeasurementType" },
                values: new object[] { null, 0 });

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333304"),
                columns: new[] { "CarbsPerUnit", "MeasurementType" },
                values: new object[] { null, 0 });

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333305"),
                columns: new[] { "CarbsPerUnit", "MeasurementType" },
                values: new object[] { null, 0 });

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333306"),
                columns: new[] { "CarbsPerUnit", "MeasurementType" },
                values: new object[] { null, 0 });

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333307"),
                columns: new[] { "CarbsPerUnit", "MeasurementType" },
                values: new object[] { null, 0 });

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333308"),
                columns: new[] { "CarbsPerUnit", "MeasurementType" },
                values: new object[] { null, 0 });

            migrationBuilder.UpdateData(
                table: "FoodItems",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333309"),
                columns: new[] { "CarbsPerUnit", "MeasurementType" },
                values: new object[] { null, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarbsPerUnitSnapshot",
                table: "MealItems");

            migrationBuilder.DropColumn(
                name: "MeasurementType",
                table: "MealItems");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "MealItems");

            migrationBuilder.DropColumn(
                name: "CarbsPerUnit",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "MeasurementType",
                table: "FoodItems");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .Annotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual")
                .Annotation("Npgsql:Enum:result_rating", "perfect,good,high_glucose,low_glucose,unknown")
                .OldAnnotation("Npgsql:Enum:food_measurement_type", "grams,portion,piece")
                .OldAnnotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .OldAnnotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual")
                .OldAnnotation("Npgsql:Enum:result_rating", "perfect,good,high_glucose,low_glucose,unknown");

            migrationBuilder.AlterColumn<decimal>(
                name: "WeightGrams",
                table: "MealItems",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,2)",
                oldPrecision: 8,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CarbsPer100gSnapshot",
                table: "MealItems",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(7,2)",
                oldPrecision: 7,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CarbsPer100g",
                table: "FoodItems",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(7,2)",
                oldPrecision: 7,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
