using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class NutritionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "calories",
                table: "recipes",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "carbohydrates",
                table: "recipes",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fat",
                table: "recipes",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "protein",
                table: "recipes",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "saturated_fat",
                table: "recipes",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "sodium",
                table: "recipes",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "sugar",
                table: "recipes",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "calories",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "carbohydrates",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "fat",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "protein",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "saturated_fat",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "sodium",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "sugar",
                table: "recipes");
        }
    }
}
