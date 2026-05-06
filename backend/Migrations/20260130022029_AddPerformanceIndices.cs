using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_items_household_id",
                table: "inventory_items");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_household_id_status",
                table: "inventory_items",
                columns: new[] { "household_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_household_id_status",
                table: "household_invitations",
                columns: new[] { "household_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_comment_likes_user_id",
                table: "comment_likes",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_items_household_id_status",
                table: "inventory_items");

            migrationBuilder.DropIndex(
                name: "IX_household_invitations_household_id_status",
                table: "household_invitations");

            migrationBuilder.DropIndex(
                name: "IX_comment_likes_user_id",
                table: "comment_likes");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_household_id",
                table: "inventory_items",
                column: "household_id");
        }
    }
}
