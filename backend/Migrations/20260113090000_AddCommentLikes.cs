using System;
using backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260113090000_AddCommentLikes")]
    public partial class AddCommentLikes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL with IF NOT EXISTS for idempotency
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS comment_likes (
                    user_id uuid NOT NULL,
                    comment_id uuid NOT NULL,
                    created_at timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT ""PK_comment_likes"" PRIMARY KEY (user_id, comment_id),
                    CONSTRAINT ""FK_comment_likes_recipe_comments_comment_id"" FOREIGN KEY (comment_id) REFERENCES recipe_comments (id) ON DELETE CASCADE,
                    CONSTRAINT ""FK_comment_likes_users_user_id"" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_comment_likes_comment_id"" ON comment_likes (comment_id);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comment_likes");
        }
    }
}
