using backend.Models;
using backend.Extensions;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<TagType> TagTypes { get; set; }
    public DbSet<Ingredient> Ingredients { get; set; }
    public DbSet<IngredientAlias> IngredientAliases { get; set; }
    public DbSet<IngredientUnit> IngredientUnits { get; set; }
    public DbSet<IngredientTag> IngredientTags { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<InventoryItemTag> InventoryItemTags { get; set; }
    public DbSet<Recipe> Recipes { get; set; }
    public DbSet<RecipeIngredient> RecipeIngredients { get; set; }
    public DbSet<RecipeIngredientTag> RecipeIngredientTags { get; set; }
    public DbSet<RecipeTag> RecipeTags { get; set; }
    public DbSet<RecipeLike> RecipeLikes { get; set; }
    public DbSet<RecipeSave> RecipeSaves { get; set; }
    public DbSet<RecipeCook> RecipeCooks { get; set; }
    public DbSet<RecipeComment> RecipeComments { get; set; }
    public DbSet<CommentLike> CommentLikes { get; set; }
    public DbSet<RecipeInteraction> RecipeInteractions { get; set; }
    public DbSet<ChecklistItem> ChecklistItems { get; set; }
    public DbSet<KnowledgebaseArticle> KnowledgebaseArticles { get; set; }
    public DbSet<Household> Households { get; set; }
    public DbSet<HouseholdMember> HouseholdMembers { get; set; }
    public DbSet<HouseholdInvitation> HouseholdInvitations { get; set; }
    public DbSet<UserPreference> UserPreferences { get; set; }
    public DbSet<NameNormalizationToken> NameNormalizationTokens { get; set; }
    public DbSet<NameNormalizationDictionaryVersion> NameNormalizationDictionaryVersions { get; set; }
    public DbSet<SmartRecipe> SmartRecipes { get; set; }
    public DbSet<SmartRecipeGenerationLog> SmartRecipeGenerationLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasDbFunction(
            typeof(PostgresFunctions).GetMethod(nameof(PostgresFunctions.Md5))!);
        modelBuilder.HasDbFunction(
            typeof(PostgresFunctions).GetMethod(nameof(PostgresFunctions.Concat))!);

        // THIS IS THE KEY LINE
        // It scans the assembly (project) where AppDbContext lives, finds
        // all IEntityTypeConfiguration classes (like UserConfiguration),
        // and applies them.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
