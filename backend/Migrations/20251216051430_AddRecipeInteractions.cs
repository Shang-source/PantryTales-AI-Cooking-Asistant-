using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recipe_interactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", maxLength: 32, nullable: false),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    dwell_seconds = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_interactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_recipe_interactions_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recipe_interactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_interactions_created_at",
                table: "recipe_interactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_interactions_event_type",
                table: "recipe_interactions",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_interactions_recipe_id",
                table: "recipe_interactions",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_interactions_recipe_id_event_type_created_at",
                table: "recipe_interactions",
                columns: new[] { "recipe_id", "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_interactions_user_id",
                table: "recipe_interactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_interactions_user_id_event_type_created_at",
                table: "recipe_interactions",
                columns: new[] { "user_id", "event_type", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recipe_interactions");
        }
    }
}
