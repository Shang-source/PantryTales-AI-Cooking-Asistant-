using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgebaseFeaturedField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_featured",
                table: "knowledgebase_articles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_knowledgebase_articles_is_featured_is_published",
                table: "knowledgebase_articles",
                columns: new[] { "is_featured", "is_published" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_knowledgebase_articles_is_featured_is_published",
                table: "knowledgebase_articles");

            migrationBuilder.DropColumn(
                name: "is_featured",
                table: "knowledgebase_articles");
        }
    }
}
