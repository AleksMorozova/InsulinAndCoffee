using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InsulinAndCoffee.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:meal_entry_source", "manual,ai_parsed,delivery,cafe")
                .Annotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .Annotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual")
                .Annotation("Npgsql:Enum:result_rating", "perfect,good,high_glucose,low_glucose,unknown")
                .OldAnnotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .OldAnnotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual")
                .OldAnnotation("Npgsql:Enum:result_rating", "perfect,good,high_glucose,low_glucose,unknown");

            migrationBuilder.CreateTable(
                name: "AiAgentInteractions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AgentResponse = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ToolsUsed = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SafetyWarnings = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAgentInteractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAgentInteractions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Carbs = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    InsulinUnits = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    MealTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    RestaurantName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Tags = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserInsulinRatios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeOfDay = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Ratio = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInsulinRatios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserInsulinRatios_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MealEntries",
                columns: new[] { "Id", "Carbs", "CreatedAtUtc", "InsulinUnits", "MealName", "MealTime", "Notes", "RestaurantName", "Source", "Tags", "UpdatedAtUtc", "UserId" },
                values: new object[,]
                {
                    { new Guid("66666666-6666-6666-6666-666666666601"), 95m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 9.5m, "Sushi set", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Demo AI agent memory.", "Sushi Master", 3, "sushi, delivery", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("66666666-6666-6666-6666-666666666602"), 70m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 7m, "Chicken shawarma", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Demo AI agent memory.", "Delivery", 3, "shawarma, delivery", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("66666666-6666-6666-6666-666666666603"), 110m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 11m, "Pizza delivery", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Demo AI agent memory.", "Pizza delivery", 3, "pizza, delivery", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("66666666-6666-6666-6666-666666666604"), 25m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2.5m, "Latte with chocolate", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Demo AI agent memory.", "Local Cafe", 4, "latte, chocolate, cafe", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("11111111-1111-1111-1111-111111111111") }
                });

            migrationBuilder.InsertData(
                table: "UserInsulinRatios",
                columns: new[] { "Id", "CreatedAtUtc", "Ratio", "TimeOfDay", "UpdatedAtUtc", "UserId" },
                values: new object[] { new Guid("55555555-5555-5555-5555-555555555501"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 10m, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.CreateIndex(
                name: "IX_AiAgentInteractions_UserId_CreatedAtUtc",
                table: "AiAgentInteractions",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MealEntries_UserId_MealName",
                table: "MealEntries",
                columns: new[] { "UserId", "MealName" });

            migrationBuilder.CreateIndex(
                name: "IX_MealEntries_UserId_MealTime",
                table: "MealEntries",
                columns: new[] { "UserId", "MealTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInsulinRatios_UserId_TimeOfDay",
                table: "UserInsulinRatios",
                columns: new[] { "UserId", "TimeOfDay" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAgentInteractions");

            migrationBuilder.DropTable(
                name: "MealEntries");

            migrationBuilder.DropTable(
                name: "UserInsulinRatios");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .Annotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual")
                .Annotation("Npgsql:Enum:result_rating", "perfect,good,high_glucose,low_glucose,unknown")
                .OldAnnotation("Npgsql:Enum:meal_entry_source", "manual,ai_parsed,delivery,cafe")
                .OldAnnotation("Npgsql:Enum:meal_type", "breakfast,lunch,dinner,snack")
                .OldAnnotation("Npgsql:Enum:reading_type", "before_meal,after_meal,manual")
                .OldAnnotation("Npgsql:Enum:result_rating", "perfect,good,high_glucose,low_glucose,unknown");
        }
    }
}
