using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InsulinAndCoffee.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .Annotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiabetesSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetGlucose = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    CarbRatio = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    CorrectionFactor = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    InsulinDurationHours = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiabetesSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiabetesSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoodItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CarbsPer100g = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    ProteinPer100g = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    FatPer100g = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    CaloriesPer100g = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoodItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealType = table.Column<int>(type: "integer", nullable: false),
                    MealTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreMealGlucose = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    TotalCarbs = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    SuggestedBolus = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    ConfirmedBolus = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GlucoseReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealId = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    ReadingTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadingType = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlucoseReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlucoseReadings_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GlucoseReadings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodNameSnapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WeightGrams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    CarbsPer100gSnapshot = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    CalculatedCarbs = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealItems_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "Name" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "aleksandra@example.com", "Aleksandra" });

            migrationBuilder.InsertData(
                table: "DiabetesSettings",
                columns: new[] { "Id", "CarbRatio", "CorrectionFactor", "InsulinDurationHours", "TargetGlucose", "UpdatedAt", "UserId" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), 10m, 3m, 4m, 6.5m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.InsertData(
                table: "FoodItems",
                columns: new[] { "Id", "CaloriesPer100g", "CarbsPer100g", "CreatedAt", "FatPer100g", "IsFavorite", "Name", "ProteinPer100g", "UserId" },
                values: new object[,]
                {
                    { new Guid("33333333-3333-3333-3333-333333333301"), 0m, 25m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0m, true, "Philadelphia Roll", 0m, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("33333333-3333-3333-3333-333333333302"), 0m, 28m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0m, false, "Sushi Rice", 0m, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("33333333-3333-3333-3333-333333333303"), 0m, 45m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0m, true, "Bread", 0m, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("33333333-3333-3333-3333-333333333304"), 0m, 1m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0m, false, "Butter", 0m, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("33333333-3333-3333-3333-333333333305"), 0m, 27m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0m, true, "Cottage Cheese Casserole", 0m, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("33333333-3333-3333-3333-333333333306"), 0m, 6m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0m, true, "Borscht", 0m, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("33333333-3333-3333-3333-333333333307"), 0m, 8m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0m, false, "Chicken Cutlet", 0m, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("33333333-3333-3333-3333-333333333308"), 0m, 5m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0m, true, "Latte", 0m, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("33333333-3333-3333-3333-333333333309"), 0m, 55m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0m, true, "Chocolate", 0m, new Guid("11111111-1111-1111-1111-111111111111") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiabetesSettings_UserId",
                table: "DiabetesSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoodItems_UserId_Name",
                table: "FoodItems",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_GlucoseReadings_MealId",
                table: "GlucoseReadings",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "IX_GlucoseReadings_UserId_ReadingTime",
                table: "GlucoseReadings",
                columns: new[] { "UserId", "ReadingTime" });

            migrationBuilder.CreateIndex(
                name: "IX_MealItems_MealId",
                table: "MealItems",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "IX_Meals_UserId_MealTime",
                table: "Meals",
                columns: new[] { "UserId", "MealTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiabetesSettings");

            migrationBuilder.DropTable(
                name: "FoodItems");

            migrationBuilder.DropTable(
                name: "GlucoseReadings");

            migrationBuilder.DropTable(
                name: "MealItems");

            migrationBuilder.DropTable(
                name: "Meals");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
