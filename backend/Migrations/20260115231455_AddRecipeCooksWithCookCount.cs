using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeCooksWithCookCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL with IF NOT EXISTS for idempotency
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS recipe_cooks (
                    id uuid NOT NULL,
                    user_id uuid NOT NULL,
                    recipe_id uuid NOT NULL,
                    cook_count integer NOT NULL DEFAULT 1,
                    first_cooked_at timestamp with time zone NOT NULL DEFAULT now(),
                    last_cooked_at timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT ""PK_recipe_cooks"" PRIMARY KEY (id),
                    CONSTRAINT ""FK_recipe_cooks_recipes_recipe_id"" FOREIGN KEY (recipe_id) REFERENCES recipes (id) ON DELETE CASCADE,
                    CONSTRAINT ""FK_recipe_cooks_users_user_id"" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_recipe_cooks_recipe_id"" ON recipe_cooks (recipe_id);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_recipe_cooks_user_id"" ON recipe_cooks (user_id);");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_recipe_cooks_user_id_recipe_id"" ON recipe_cooks (user_id, recipe_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recipe_cooks");
        }
    }
}
