using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSmartRecipeFromHouseholdToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear existing smart_recipes as they are cached data keyed by household_id
            // and will be regenerated per-user on next access
            migrationBuilder.Sql("DELETE FROM smart_recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_smart_recipes_households_household_id",
                table: "smart_recipes");

            migrationBuilder.RenameColumn(
                name: "household_id",
                table: "smart_recipes",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "IX_smart_recipes_household_id",
                table: "smart_recipes",
                newName: "IX_smart_recipes_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_smart_recipes_users_user_id",
                table: "smart_recipes",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_smart_recipes_users_user_id",
                table: "smart_recipes");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "smart_recipes",
                newName: "household_id");

            migrationBuilder.RenameIndex(
                name: "IX_smart_recipes_user_id",
                table: "smart_recipes",
                newName: "IX_smart_recipes_household_id");

            migrationBuilder.AddForeignKey(
                name: "FK_smart_recipes_households_household_id",
                table: "smart_recipes",
                column: "household_id",
                principalTable: "households",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
