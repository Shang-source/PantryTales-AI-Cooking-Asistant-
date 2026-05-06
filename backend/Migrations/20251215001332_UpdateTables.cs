using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "name_normalization_algorithm_version",
                table: "inventory_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "name_normalization_dictionary_version",
                table: "inventory_items",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_normalization_removed_tokens",
                table: "inventory_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "name_normalization_algorithm_version",
                table: "ingredient_aliases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "name_normalization_dictionary_version",
                table: "ingredient_aliases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_normalization_removed_tokens",
                table: "ingredient_aliases",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "name_normalization_dictionary_versions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    dictionary_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    algorithm_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_name_normalization_dictionary_versions", x => x.id);
                    table.CheckConstraint("CK_name_normalization_dictionary_versions_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "name_normalization_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    category = table.Column<string>(type: "text", maxLength: 32, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_name_normalization_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_name_normalization_tokens_category",
                table: "name_normalization_tokens",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_name_normalization_tokens_is_active",
                table: "name_normalization_tokens",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_name_normalization_tokens_token_category",
                table: "name_normalization_tokens",
                columns: new[] { "token", "category" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "name_normalization_dictionary_versions");

            migrationBuilder.DropTable(
                name: "name_normalization_tokens");

            migrationBuilder.DropColumn(
                name: "name_normalization_algorithm_version",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "name_normalization_dictionary_version",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "name_normalization_removed_tokens",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "name_normalization_algorithm_version",
                table: "ingredient_aliases");

            migrationBuilder.DropColumn(
                name: "name_normalization_dictionary_version",
                table: "ingredient_aliases");

            migrationBuilder.DropColumn(
                name: "name_normalization_removed_tokens",
                table: "ingredient_aliases");
        }
    }
}
