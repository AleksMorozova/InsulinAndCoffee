using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsulinAndCoffee.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameKnownMealsToDeliveryMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnownMeals_Users_UserId",
                table: "KnownMeals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_KnownMeals",
                table: "KnownMeals");

            migrationBuilder.RenameTable(
                name: "KnownMeals",
                newName: "DeliveryMeals");

            migrationBuilder.RenameIndex(
                name: "IX_KnownMeals_UserId_UsageCount",
                table: "DeliveryMeals",
                newName: "IX_DeliveryMeals_UserId_UsageCount");

            migrationBuilder.RenameIndex(
                name: "IX_KnownMeals_UserId_PlaceName_DishName",
                table: "DeliveryMeals",
                newName: "IX_DeliveryMeals_UserId_PlaceName_DishName");

            migrationBuilder.RenameIndex(
                name: "IX_KnownMeals_UserId_IsFavorite",
                table: "DeliveryMeals",
                newName: "IX_DeliveryMeals_UserId_IsFavorite");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DeliveryMeals",
                table: "DeliveryMeals",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryMeals_Users_UserId",
                table: "DeliveryMeals",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryMeals_Users_UserId",
                table: "DeliveryMeals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DeliveryMeals",
                table: "DeliveryMeals");

            migrationBuilder.RenameTable(
                name: "DeliveryMeals",
                newName: "KnownMeals");

            migrationBuilder.RenameIndex(
                name: "IX_DeliveryMeals_UserId_UsageCount",
                table: "KnownMeals",
                newName: "IX_KnownMeals_UserId_UsageCount");

            migrationBuilder.RenameIndex(
                name: "IX_DeliveryMeals_UserId_PlaceName_DishName",
                table: "KnownMeals",
                newName: "IX_KnownMeals_UserId_PlaceName_DishName");

            migrationBuilder.RenameIndex(
                name: "IX_DeliveryMeals_UserId_IsFavorite",
                table: "KnownMeals",
                newName: "IX_KnownMeals_UserId_IsFavorite");

            migrationBuilder.AddPrimaryKey(
                name: "PK_KnownMeals",
                table: "KnownMeals",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_KnownMeals_Users_UserId",
                table: "KnownMeals",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
