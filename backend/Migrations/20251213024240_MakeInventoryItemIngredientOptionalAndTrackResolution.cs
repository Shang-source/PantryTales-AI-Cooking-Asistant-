using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class MakeInventoryItemIngredientOptionalAndTrackResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ingredient_id",
                table: "inventory_items",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ingredient_last_resolve_error",
                table: "inventory_items",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ingredient_resolve_attempts",
                table: "inventory_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ingredient_resolve_confidence",
                table: "inventory_items",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ingredient_resolve_method",
                table: "inventory_items",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "ingredient_resolve_status",
                table: "inventory_items",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<DateTime>(
                name: "ingredient_resolved_at",
                table: "inventory_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_name",
                table: "inventory_items",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE inventory_items
                SET ingredient_resolve_status = 2,
                    ingredient_resolved_at = COALESCE(ingredient_resolved_at, now())
                WHERE ingredient_id IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_ingredient_resolve_status_ingredient_resolv~",
                table: "inventory_items",
                columns: new[] { "ingredient_resolve_status", "ingredient_resolve_attempts" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_normalized_name",
                table: "inventory_items",
                column: "normalized_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_items_ingredient_resolve_status_ingredient_resolv~",
                table: "inventory_items");

            migrationBuilder.DropIndex(
                name: "IX_inventory_items_normalized_name",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "ingredient_last_resolve_error",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "ingredient_resolve_attempts",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "ingredient_resolve_confidence",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "ingredient_resolve_method",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "ingredient_resolve_status",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "ingredient_resolved_at",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "normalized_name",
                table: "inventory_items");

            migrationBuilder.AlterColumn<Guid>(
                name: "ingredient_id",
                table: "inventory_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
