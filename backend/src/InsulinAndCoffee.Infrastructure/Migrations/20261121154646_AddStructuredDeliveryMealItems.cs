using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsulinAndCoffee.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredDeliveryMealItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "DeliveryMeals",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "DeliveryMealItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    MeasurementType = table.Column<int>(type: "integer", nullable: false),
                    CarbsPer100gSnapshot = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    CarbsPerUnitSnapshot = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    CalculatedCarbs = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    CarbOverride = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    CarbReferenceSource = table.Column<int>(type: "integer", nullable: false),
                    CarbTransformation = table.Column<int>(type: "integer", nullable: false),
                    SourceMealVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryMealItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryMealItems_DeliveryMeals_DeliveryMealId",
                        column: x => x.DeliveryMealId,
                        principalTable: "DeliveryMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryMealItems_FoodItems_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryMealItems_DeliveryMealId_DisplayOrder",
                table: "DeliveryMealItems",
                columns: new[] { "DeliveryMealId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryMealItems_FoodItemId",
                table: "DeliveryMealItems",
                column: "FoodItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryMealItems");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "DeliveryMeals");
        }
    }
}
