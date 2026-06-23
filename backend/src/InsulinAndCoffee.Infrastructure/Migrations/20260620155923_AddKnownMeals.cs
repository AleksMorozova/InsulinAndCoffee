using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsulinAndCoffee.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnownMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .Annotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual")
                .Annotation("Npgsql:Enum:result_rating", "perfect,good,high_glucose,low_glucose,unknown")
                .OldAnnotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .OldAnnotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual");

            migrationBuilder.CreateTable(
                name: "KnownMeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaceName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DishName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    PortionDescription = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Carbs = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    UsualInsulinUnits = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    LastPreMealGlucose = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    ResultRating = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownMeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnownMeals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnownMeals_UserId_IsFavorite",
                table: "KnownMeals",
                columns: new[] { "UserId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "IX_KnownMeals_UserId_PlaceName_DishName",
                table: "KnownMeals",
                columns: new[] { "UserId", "PlaceName", "DishName" });

            migrationBuilder.CreateIndex(
                name: "IX_KnownMeals_UserId_UsageCount",
                table: "KnownMeals",
                columns: new[] { "UserId", "UsageCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnownMeals");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .Annotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual")
                .OldAnnotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .OldAnnotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual")
                .OldAnnotation("Npgsql:Enum:result_rating", "perfect,good,high_glucose,low_glucose,unknown");
        }
    }
}
