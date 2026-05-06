using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIngredientAliasResolutionAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ingredient_aliases_ingredients_ingredient_id",
                table: "ingredient_aliases");

            migrationBuilder.DropIndex(
                name: "IX_ingredient_aliases_ingredient_id_alias_name",
                table: "ingredient_aliases");

            migrationBuilder.AlterColumn<Guid>(
                name: "ingredient_id",
                table: "ingredient_aliases",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "ingredient_aliases",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "normalized_name",
                table: "ingredient_aliases",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolve_method",
                table: "ingredient_aliases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at",
                table: "ingredient_aliases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "status",
                table: "ingredient_aliases",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "ingredient_aliases",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_aliases_ingredient_id",
                table: "ingredient_aliases",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_aliases_normalized_name",
                table: "ingredient_aliases",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_aliases_source_normalized_name",
                table: "ingredient_aliases",
                columns: new[] { "source", "normalized_name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ingredient_aliases_ingredients_ingredient_id",
                table: "ingredient_aliases",
                column: "ingredient_id",
                principalTable: "ingredients",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ingredient_aliases_ingredients_ingredient_id",
                table: "ingredient_aliases");

            migrationBuilder.DropIndex(
                name: "IX_ingredient_aliases_ingredient_id",
                table: "ingredient_aliases");

            migrationBuilder.DropIndex(
                name: "IX_ingredient_aliases_normalized_name",
                table: "ingredient_aliases");

            migrationBuilder.DropIndex(
                name: "IX_ingredient_aliases_source_normalized_name",
                table: "ingredient_aliases");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "ingredient_aliases");

            migrationBuilder.DropColumn(
                name: "normalized_name",
                table: "ingredient_aliases");

            migrationBuilder.DropColumn(
                name: "resolve_method",
                table: "ingredient_aliases");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                table: "ingredient_aliases");

            migrationBuilder.DropColumn(
                name: "status",
                table: "ingredient_aliases");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "ingredient_aliases");

            migrationBuilder.AlterColumn<Guid>(
                name: "ingredient_id",
                table: "ingredient_aliases",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_aliases_ingredient_id_alias_name",
                table: "ingredient_aliases",
                columns: new[] { "ingredient_id", "alias_name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ingredient_aliases_ingredients_ingredient_id",
                table: "ingredient_aliases",
                column: "ingredient_id",
                principalTable: "ingredients",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
