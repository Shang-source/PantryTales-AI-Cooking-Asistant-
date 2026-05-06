using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Security.Claims;
using System.Threading.Tasks;
using backend.Data;
using backend.Dtos.Recipes;
using backend.Interfaces;
using backend.Models;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Services;

public class RecipeServiceTests
{
    private const string ClerkUserId = "clerk_test_user";

    [Fact]
    public async Task CreateAsync_ReturnsUserNotFound_WhenUserMissing()
    {
        await using var context = CreateContext();
        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);

        var result = await service.CreateAsync(BuildRequest(), ClerkUserId, CancellationToken.None);

        Assert.Equal(CreateRecipeResultStatus.UserNotFound, result.Status);
        Assert.Null(result.Recipe);
        Assert.Empty(context.Recipes);
    }

    [Fact]
    public async Task CreateAsync_ReturnsHouseholdNotFound_WhenUserHasNoMembership()
    {
        await using var context = CreateContext();
        context.Users.Add(BuildUser());
        await context.SaveChangesAsync();
        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);

        var result = await service.CreateAsync(BuildRequest(), ClerkUserId, CancellationToken.None);

        Assert.Equal(CreateRecipeResultStatus.HouseholdNotFound, result.Status);
        Assert.Null(result.Recipe);
        Assert.Empty(context.Recipes);
    }

    [Fact]
    public async Task CreateAsync_ReturnsInvalidRequest_WhenNoValidSteps()
    {
        await using var context = CreateContext();
        SeedHouseholdWithUser(context);
        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);
        var request = BuildRequest();
        request.Steps = ["   ", "", "\t"];

        var result = await service.CreateAsync(request, ClerkUserId, CancellationToken.None);

        Assert.Equal(CreateRecipeResultStatus.InvalidRequest, result.Status);
        Assert.Null(result.Recipe);
        Assert.Empty(context.Recipes);
    }

    [Fact]
    public async Task CreateAsync_CreatesRecipe_AndTags()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);

        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);
        var request = BuildRequest();
        request.Title = "  Fancy Pie  ";
        request.Description = "  Tasty dessert ";
        request.Servings = 4;
        request.TotalTimeMinutes = 90;
        request.Difficulty = RecipeDifficulty.Hard;
        request.ImageUrls = [" https://example.com/pie.jpg ", ""];
        request.Steps = [" Prep ", "  ", "\t", "Bake"];
        request.Tags = [" Dinner ", "dinner", " Vegan "];

        var result = await service.CreateAsync(request, ClerkUserId, CancellationToken.None);

        Assert.Equal(CreateRecipeResultStatus.Success, result.Status);
        Assert.NotNull(result.Recipe);
        Assert.Equal("Fancy Pie", result.Recipe!.Title);
        Assert.Equal("Tasty dessert", result.Recipe.Description);
        Assert.Equal(RecipeVisibility.Private, result.Recipe.Visibility);
        Assert.Equal(RecipeDifficulty.Hard, result.Recipe.Difficulty);
        Assert.Equal(4, result.Recipe.Servings);
        Assert.Equal(90, result.Recipe.TotalTimeMinutes);
        Assert.Equal(household.Id, result.Recipe.HouseholdId);
        Assert.Single(result.Recipe.ImageUrls ?? []);
        Assert.Equal("https://example.com/pie.jpg", result.Recipe.ImageUrls![0]);
        Assert.Equal(["Prep", "Bake"], result.Recipe.Steps);
        Assert.Equal(["Dinner", "Vegan"], result.Recipe.Tags);

        var recipe = Assert.Single(await context.Recipes
            .AsNoTracking()
            .Include(r => r.Tags).ThenInclude(rt => rt.Tag)
            .ToListAsync());
        Assert.Equal("fancy pie", recipe.Title.ToLowerInvariant());

        var tags = await context.Tags.OrderBy(t => t.Name).ToListAsync();
        Assert.Equal(2, tags.Count);
        Assert.All(tags, t => Assert.Equal("recipe", t.Type));
        Assert.Equal("dinner", tags[0].Name);
        Assert.Equal("Dinner", tags[0].DisplayName);
        Assert.Equal("vegan", tags[1].Name);
        Assert.Equal("Vegan", tags[1].DisplayName);

        var recipeTags = await context.RecipeTags.ToListAsync();
        Assert.Equal(2, recipeTags.Count);
        Assert.Equal(2, recipe.Tags.Select(rt => rt.Tag?.Name).Distinct().Count());
    }

    [Fact]
    public async Task ListAsync_CommunityScope_ReturnsOnlyPublicUserRecipes()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var user = context.Users.Single(u => u.ClerkUserId == ClerkUserId);
        var tag = BuildTag("Cheese", "cheese");
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        var publicRecipe = BuildRecipe(household.Id, user.Id, "Public Recipe", RecipeVisibility.Public, RecipeType.User,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(-1));
        var privateRecipe = BuildRecipe(household.Id, user.Id, "Private Recipe", RecipeVisibility.Private, RecipeType.User,
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-2));
        var systemRecipe = BuildRecipe(household.Id, user.Id, "System Recipe", RecipeVisibility.Public, RecipeType.System,
            DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-3));

        context.Recipes.AddRange(publicRecipe, privateRecipe, systemRecipe);
        context.RecipeTags.Add(new RecipeTag { RecipeId = publicRecipe.Id, TagId = tag.Id });
        await context.SaveChangesAsync();

        var service = CreateService(context, new TestRecipeRepository(context));
        var result = await service.ListAsync("community", new ClaimsPrincipal(), CancellationToken.None);

        Assert.Equal(RecipeListResultStatus.Success, result.Status);
        var cards = Assert.Single(result.Recipes ?? Array.Empty<RecipeCardDto>());
        Assert.Equal(publicRecipe.Id, cards.Id);
        Assert.Equal("Cheese", Assert.Single(cards.Tags));
    }

    [Fact]
    public async Task CreateAsync_ReusesExistingTags_IgnoresCaseAndWhitespace()
    {
        await using var context = CreateContext();
        SeedHouseholdWithUser(context);
        context.Tags.Add(BuildTag("Dinner", "dinner"));
        await context.SaveChangesAsync();

        var service = CreateService(context, new TestRecipeRepository(context));
        var request = BuildRequest();
        request.Tags = [" DINNER ", "dinner", "Dinner"];

        var result = await service.CreateAsync(request, ClerkUserId, CancellationToken.None);

        Assert.Equal(CreateRecipeResultStatus.Success, result.Status);
        Assert.Single(await context.Tags.ToListAsync());
        Assert.Single(await context.RecipeTags.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_UpdatesExistingDisplayName_WhenDifferent()
    {
        await using var context = CreateContext();
        SeedHouseholdWithUser(context);
        var tag = new Tag
        {
            Name = "dinner",
            DisplayName = "dinner",
            Type = "recipe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        var service = CreateService(context, new TestRecipeRepository(context));
        var request = BuildRequest();
        request.Tags = ["dinner"];

        var result = await service.CreateAsync(request, ClerkUserId, CancellationToken.None);

        Assert.Equal(CreateRecipeResultStatus.Success, result.Status);
        var updated = await context.Tags.SingleAsync(t => t.Name == "dinner");
        Assert.Equal("Dinner", updated.DisplayName);
    }

    [Fact]
    public async Task CreateAsync_CreatesSpecialCharacterTags_WithNormalizedName()
    {
        await using var context = CreateContext();
        SeedHouseholdWithUser(context);
        var service = CreateService(context, new TestRecipeRepository(context));

        var request = BuildRequest();
        request.Tags = [" home-style "];

        var result = await service.CreateAsync(request, ClerkUserId, CancellationToken.None);

        Assert.Equal(CreateRecipeResultStatus.Success, result.Status);
        var tag = await context.Tags.SingleAsync();
        Assert.Equal("home-style", tag.Name);
        Assert.Equal("Home-style", tag.DisplayName);
    }

    [Fact]
    public async Task ListAsync_MeScope_ReturnsAuthorRecipesOrderedByUpdatedAt()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var user = context.Users.Single(u => u.ClerkUserId == ClerkUserId);
        var tag = BuildTag("Meals", "meals");
        context.Tags.Add(tag);
        var otherUser = new User { ClerkUserId = "other", Nickname = "Other", Email = "other@example.com" };
        context.Users.Add(otherUser);
        await context.SaveChangesAsync();

        var earlierRecipe = BuildRecipe(household.Id, user.Id, "Earlier", RecipeVisibility.Public, RecipeType.User,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
        var laterRecipe = BuildRecipe(household.Id, user.Id, "Later", RecipeVisibility.Private, RecipeType.User,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        var otherRecipe = BuildRecipe(household.Id, otherUser.Id, "Other", RecipeVisibility.Public, RecipeType.User,
            DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-3));

        context.Recipes.AddRange(earlierRecipe, laterRecipe, otherRecipe);
        context.RecipeTags.Add(new RecipeTag { RecipeId = earlierRecipe.Id, TagId = tag.Id });
        context.RecipeTags.Add(new RecipeTag { RecipeId = laterRecipe.Id, TagId = tag.Id });
        await context.SaveChangesAsync();

        var service = CreateService(context, new TestRecipeRepository(context));
        var principal = BuildPrincipal(ClerkUserId);

        var result = await service.ListAsync("me", principal, CancellationToken.None);

        Assert.Equal(RecipeListResultStatus.Success, result.Status);
        var cards = result.Recipes ?? Array.Empty<RecipeCardDto>();
        Assert.Equal(2, cards.Count);
        Assert.Equal(laterRecipe.Id, cards[0].Id);
        Assert.Equal(earlierRecipe.Id, cards[1].Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRecipeAndRelations_WhenAuthorMatches()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var user = context.Users.Single(u => u.ClerkUserId == ClerkUserId);
        var recipe = BuildRecipe(household.Id, user.Id, "Disposable", RecipeVisibility.Public, RecipeType.User,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(-1));
        context.Recipes.Add(recipe);
        var tag = BuildTag("Throwaway", "throwaway");
        context.Tags.Add(tag);
        await context.SaveChangesAsync();
        context.RecipeTags.Add(new RecipeTag { RecipeId = recipe.Id, TagId = tag.Id });
        await context.SaveChangesAsync();

        var service = CreateService(context, new TestRecipeRepository(context));
        var result = await service.DeleteAsync(recipe.Id, ClerkUserId, CancellationToken.None);

        Assert.Equal(DeleteRecipeResultStatus.Success, result.Status);
        Assert.Empty(await context.Recipes.ToListAsync());
        Assert.Empty(await context.RecipeTags.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsUnauthorized_WhenAuthorDiffers()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var otherUser = new User { ClerkUserId = "other", Nickname = "Other", Email = "other@example.com" };
        context.Users.Add(otherUser);
        context.Recipes.Add(BuildRecipe(household.Id, otherUser.Id, "OtherRecipe", RecipeVisibility.Public, RecipeType.User,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(-1)));
        await context.SaveChangesAsync();

        var service = CreateService(context, new TestRecipeRepository(context));
        var recipeId = context.Recipes.First().Id;

        var result = await service.DeleteAsync(recipeId, ClerkUserId, CancellationToken.None);

        Assert.Equal(DeleteRecipeResultStatus.Unauthorized, result.Status);
        Assert.Single(await context.Recipes.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsRecipeNotFound_WhenTypeIsNotUser()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var user = context.Users.Single(u => u.ClerkUserId == ClerkUserId);
        var recipe = BuildRecipe(household.Id, user.Id, "System", RecipeVisibility.Public, RecipeType.System,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();

        var service = CreateService(context, new TestRecipeRepository(context));
        var result = await service.DeleteAsync(recipe.Id, ClerkUserId, CancellationToken.None);

        Assert.Equal(DeleteRecipeResultStatus.RecipeNotFound, result.Status);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsRecipeNotFound_WhenRecipeMissing()
    {
        await using var context = CreateContext();
        var service = CreateService(context, new TestRecipeRepository(context));

        var result = await service.GetByIdAsync(Guid.NewGuid(), new ClaimsPrincipal(), CancellationToken.None);

        Assert.Equal(RecipeDetailResultStatus.RecipeNotFound, result.Status);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUnauthorized_WhenPrivateRecipeOwnedByAnotherUser()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var differentUser = new User
        {
            ClerkUserId = "someone_else",
            Nickname = "Other",
            Email = "other@example.com"
        };
        context.Users.Add(differentUser);
        await context.SaveChangesAsync();
        var recipe = BuildRecipe(household.Id, differentUser.Id, "Secret",
            RecipeVisibility.Private, RecipeType.User, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();

        var service = CreateService(context, new TestRecipeRepository(context));
        var principal = BuildPrincipal(ClerkUserId);

        var result = await service.GetByIdAsync(recipe.Id, principal, CancellationToken.None);

        Assert.Equal(RecipeDetailResultStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsSuccess_ForPublicRecipe()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var author = context.Users.Single(u => u.ClerkUserId == ClerkUserId);
        var recipe = BuildRecipe(household.Id, author.Id, "Public Dish", RecipeVisibility.Public, RecipeType.User,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
        recipe.Steps = "[\"Prep\",\"Cook\"]";
        recipe.ImageUrls = ["https://example.com/image.jpg"];
        context.Recipes.Add(recipe);
        var tag = BuildTag("Vegan", "vegan");
        context.Tags.Add(tag);
        await context.SaveChangesAsync();
        context.RecipeTags.Add(new RecipeTag { RecipeId = recipe.Id, TagId = tag.Id });
        await context.SaveChangesAsync();

        var service = CreateService(context, new TestRecipeRepository(context));

        var result = await service.GetByIdAsync(recipe.Id, new ClaimsPrincipal(), CancellationToken.None);

        Assert.Equal(RecipeDetailResultStatus.Success, result.Status);
        Assert.NotNull(result.Recipe);
        var detail = result.Recipe!;
        Assert.Equal(recipe.Id, detail.Id);
        Assert.Equal(recipe.HouseholdId, detail.HouseholdId);
        Assert.Equal(author.Id, detail.AuthorId);
        Assert.NotNull(detail.Author);
        Assert.Equal(author.Nickname, detail.Author!.Nickname);
        Assert.Equal(recipe.Visibility, detail.Visibility);
        Assert.Equal(recipe.ImageUrls, detail.ImageUrls);
        Assert.Equal(["Prep", "Cook"], detail.Steps);
        Assert.Contains("Vegan", detail.Tags);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsUserNotFound_WhenUserMissing()
    {
        await using var context = CreateContext();
        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);

        var result = await service.UpdateAsync(Guid.NewGuid(), BuildRequest(), ClerkUserId, CancellationToken.None);

        Assert.Equal(UpdateRecipeResultStatus.UserNotFound, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsRecipeNotFound_WhenRecipeMissing()
    {
        await using var context = CreateContext();
        SeedHouseholdWithUser(context);
        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);

        var result = await service.UpdateAsync(Guid.NewGuid(), BuildRequest(), ClerkUserId, CancellationToken.None);

        Assert.Equal(UpdateRecipeResultStatus.RecipeNotFound, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsUnauthorized_WhenNotAuthor()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var otherUser = new User { ClerkUserId = "other", Nickname = "Other", Email = "other@example.com" };
        context.Users.Add(otherUser);
        var recipe = BuildRecipe(household.Id, otherUser.Id, "OtherRecipe", RecipeVisibility.Public, RecipeType.User,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(-1));
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();

        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);

        var result = await service.UpdateAsync(recipe.Id, BuildRequest(), ClerkUserId, CancellationToken.None);

        Assert.Equal(UpdateRecipeResultStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesRecipeFields_Successfully()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var user = context.Users.Single(u => u.ClerkUserId == ClerkUserId);
        var recipe = BuildRecipe(household.Id, user.Id, "Original", RecipeVisibility.Private, RecipeType.User,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(-1));
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();

        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);
        var request = new CreateRecipeRequestDto
        {
            Title = "Updated Title",
            Description = "Updated Description",
            Visibility = RecipeVisibility.Public,
            Steps = ["New Step 1", "New Step 2"],
            Difficulty = RecipeDifficulty.Medium,
            Servings = 6,
            TotalTimeMinutes = 45
        };

        var result = await service.UpdateAsync(recipe.Id, request, ClerkUserId, CancellationToken.None);

        Assert.Equal(UpdateRecipeResultStatus.Success, result.Status);
        Assert.NotNull(result.Recipe);
        Assert.Equal("Updated Title", result.Recipe!.Title);
        Assert.Equal("Updated Description", result.Recipe.Description);
        Assert.Equal(RecipeVisibility.Public, result.Recipe.Visibility);
        Assert.Equal(RecipeDifficulty.Medium, result.Recipe.Difficulty);
        Assert.Equal(6, result.Recipe.Servings);
        Assert.Equal(45, result.Recipe.TotalTimeMinutes);
        Assert.Equal(["New Step 1", "New Step 2"], result.Recipe.Steps);
    }

    [Fact]
    public async Task UpdateAsync_AddsIngredients_WhenProvided()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var user = context.Users.Single(u => u.ClerkUserId == ClerkUserId);
        var recipe = BuildRecipe(household.Id, user.Id, "Recipe", RecipeVisibility.Private, RecipeType.User,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(-1));
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();

        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);
        var request = BuildRequest();
        request.Ingredients =
        [
            new CreateRecipeIngredientDto { Name = "Flour", Amount = 2, Unit = "cups", IsOptional = false },
            new CreateRecipeIngredientDto { Name = "Sugar", Amount = 1, Unit = "tbsp", IsOptional = true }
        ];

        var result = await service.UpdateAsync(recipe.Id, request, ClerkUserId, CancellationToken.None);

        Assert.Equal(UpdateRecipeResultStatus.Success, result.Status);

        var recipeIngredients = await context.RecipeIngredients
            .Where(ri => ri.RecipeId == recipe.Id)
            .ToListAsync();
        Assert.Equal(2, recipeIngredients.Count);

        var ingredients = await context.Ingredients.ToListAsync();
        Assert.Equal(2, ingredients.Count);
        Assert.Contains(ingredients, i => i.CanonicalName.ToLower() == "flour");
        Assert.Contains(ingredients, i => i.CanonicalName.ToLower() == "sugar");
    }

    [Fact]
    public async Task UpdateAsync_ReplacesExistingIngredients()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var user = context.Users.Single(u => u.ClerkUserId == ClerkUserId);
        var recipe = BuildRecipe(household.Id, user.Id, "Recipe", RecipeVisibility.Private, RecipeType.User,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(-1));
        context.Recipes.Add(recipe);

        // Add existing ingredient
        var existingIngredient = new Ingredient { CanonicalName = "Old Ingredient" };
        context.Ingredients.Add(existingIngredient);
        await context.SaveChangesAsync();

        context.RecipeIngredients.Add(new backend.Models.RecipeIngredient
        {
            Id = Guid.CreateVersion7(),
            RecipeId = recipe.Id,
            IngredientId = existingIngredient.Id,
            Amount = 1,
            Unit = "pcs",
            IsOptional = false,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var initialRecipeIngredients = await context.RecipeIngredients
            .Where(ri => ri.RecipeId == recipe.Id)
            .ToListAsync();
        Assert.Single(initialRecipeIngredients);

        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);
        var request = BuildRequest();
        request.Ingredients =
        [
            new CreateRecipeIngredientDto { Name = "New Ingredient 1", Amount = 3, Unit = "cups" },
            new CreateRecipeIngredientDto { Name = "New Ingredient 2", Amount = 2, Unit = "tbsp" }
        ];

        var result = await service.UpdateAsync(recipe.Id, request, ClerkUserId, CancellationToken.None);

        Assert.Equal(UpdateRecipeResultStatus.Success, result.Status);

        var updatedRecipeIngredients = await context.RecipeIngredients
            .Where(ri => ri.RecipeId == recipe.Id)
            .Include(ri => ri.Ingredient)
            .ToListAsync();
        Assert.Equal(2, updatedRecipeIngredients.Count);
        Assert.DoesNotContain(updatedRecipeIngredients, ri => ri.Ingredient?.CanonicalName == "Old Ingredient");
        Assert.Contains(updatedRecipeIngredients, ri => ri.Ingredient?.CanonicalName.ToLower() == "new ingredient 1");
        Assert.Contains(updatedRecipeIngredients, ri => ri.Ingredient?.CanonicalName.ToLower() == "new ingredient 2");
    }

    [Fact]
    public async Task UpdateAsync_ReusesExistingIngredient_WhenNameMatches()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var user = context.Users.Single(u => u.ClerkUserId == ClerkUserId);
        var recipe = BuildRecipe(household.Id, user.Id, "Recipe", RecipeVisibility.Private, RecipeType.User,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(-1));
        context.Recipes.Add(recipe);

        // Add existing ingredient in database
        var existingIngredient = new Ingredient { CanonicalName = "Flour", DefaultUnit = "g" };
        context.Ingredients.Add(existingIngredient);
        await context.SaveChangesAsync();

        var initialIngredientCount = await context.Ingredients.CountAsync();
        Assert.Equal(1, initialIngredientCount);

        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);
        var request = BuildRequest();
        request.Ingredients =
        [
            new CreateRecipeIngredientDto { Name = "flour", Amount = 2, Unit = "cups" } // lowercase to test case-insensitivity
        ];

        var result = await service.UpdateAsync(recipe.Id, request, ClerkUserId, CancellationToken.None);

        Assert.Equal(UpdateRecipeResultStatus.Success, result.Status);

        // Should not create a new ingredient
        var finalIngredientCount = await context.Ingredients.CountAsync();
        Assert.Equal(1, finalIngredientCount);

        // Recipe ingredient should reference the existing ingredient
        var recipeIngredient = await context.RecipeIngredients
            .Where(ri => ri.RecipeId == recipe.Id)
            .SingleAsync();
        Assert.Equal(existingIngredient.Id, recipeIngredient.IngredientId);
    }

    [Fact]
    public async Task UpdateAsync_ClearsIngredients_WhenEmptyListProvided()
    {
        await using var context = CreateContext();
        var household = SeedHouseholdWithUser(context);
        var user = context.Users.Single(u => u.ClerkUserId == ClerkUserId);
        var recipe = BuildRecipe(household.Id, user.Id, "Recipe", RecipeVisibility.Private, RecipeType.User,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(-1));
        context.Recipes.Add(recipe);

        var ingredient = new Ingredient { CanonicalName = "Ingredient" };
        context.Ingredients.Add(ingredient);
        await context.SaveChangesAsync();

        context.RecipeIngredients.Add(new backend.Models.RecipeIngredient
        {
            Id = Guid.CreateVersion7(),
            RecipeId = recipe.Id,
            IngredientId = ingredient.Id,
            Amount = 1,
            Unit = "pcs",
            IsOptional = false,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repo = new TestRecipeRepository(context);
        var service = CreateService(context, repo);
        var request = BuildRequest();
        request.Ingredients = []; // Empty list

        var result = await service.UpdateAsync(recipe.Id, request, ClerkUserId, CancellationToken.None);

        Assert.Equal(UpdateRecipeResultStatus.Success, result.Status);

        var recipeIngredients = await context.RecipeIngredients
            .Where(ri => ri.RecipeId == recipe.Id)
            .ToListAsync();
        Assert.Empty(recipeIngredients);
    }

    private static RecipeService CreateService(AppDbContext context, IRecipeRepository repo) =>
        new(context, repo, NullLogger<RecipeService>.Instance);

    private static CreateRecipeRequestDto BuildRequest() =>
        new()
        {
            Title = "Test Recipe",
            Description = "Description",
            Visibility = RecipeVisibility.Private,
            Steps = ["Step1"]
        };

    private static User BuildUser() => new()
    {
        ClerkUserId = ClerkUserId,
        Nickname = "Tester",
        Email = "test@example.com"
    };

    private static Household SeedHouseholdWithUser(AppDbContext context)
    {
        var user = BuildUser();
        var household = new Household { Name = "Home", OwnerId = user.Id };
        var member = new HouseholdMember
        {
            HouseholdId = household.Id,
            Household = household,
            UserId = user.Id,
            User = user,
            Role = "owner",
            DisplayName = user.Nickname,
            Email = user.Email,
            JoinedAt = DateTime.UtcNow.AddDays(-1)
        };

        user.HouseholdMemberships.Add(member);
        household.Members.Add(member);

        context.Users.Add(user);
        context.Households.Add(household);
        context.HouseholdMembers.Add(member);
        context.SaveChanges();

        return household;
    }

    private static ClaimsPrincipal BuildPrincipal(string clerkUserId) =>
        new(new ClaimsIdentity([new Claim("clerk_user_id", clerkUserId)], "tests"));

    private static Recipe BuildRecipe(Guid householdId, Guid authorId, string title,
        RecipeVisibility visibility, RecipeType type, DateTime createdAt, DateTime updatedAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            HouseholdId = householdId,
            AuthorId = authorId,
            Title = title,
            Description = title,
            Steps = "[]",
            Visibility = visibility,
            Type = type,
            LikesCount = 0,
            CommentsCount = 0,
            SavedCount = 0,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Difficulty = RecipeDifficulty.None
        };

    private static Tag BuildTag(string rawName, string normalizedDisplayName) =>
        new()
        {
            Name = normalizedDisplayName,
            DisplayName = char.ToUpperInvariant(normalizedDisplayName[0]) + normalizedDisplayName[1..],
            Type = "recipe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static TestRecipeDbContext CreateContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TestRecipeDbContext(options, connection);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class TestRecipeRepository : IRecipeRepository
    {
        private readonly AppDbContext _context;

        public TestRecipeRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Recipe?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default) =>
            _context.Recipes.FindAsync(new object[] { recipeId }, cancellationToken).AsTask();

        public Task<Recipe?> GetDetailedByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
        {
            return _context.Recipes
                .AsSplitQuery()
                .Include(r => r.Author)
                .Include(r => r.Tags).ThenInclude(rt => rt.Tag)
                .SingleOrDefaultAsync(r => r.Id == recipeId, cancellationToken);
        }

        public Task<List<Recipe>> GetFeaturedRecipesAsync(int count, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Recipe>());

        public Task ToggleFeaturedAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestRecipeDbContext : AppDbContext
    {
        private readonly SqliteConnection _connection;

        public TestRecipeDbContext(DbContextOptions<AppDbContext> options, SqliteConnection connection) : base(options)
        {
            _connection = connection;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>();
            modelBuilder.Entity<Household>();
            modelBuilder.Entity<HouseholdMember>()
                .HasKey(m => new { m.HouseholdId, m.UserId });
            modelBuilder.Entity<User>()
                .Ignore(u => u.Embedding);
            modelBuilder.Entity<Ingredient>()
                .Ignore(i => i.Embedding);
            modelBuilder.Entity<Recipe>()
                .Ignore(r => r.Embedding);
            modelBuilder.Entity<backend.Models.RecipeIngredient>();
            modelBuilder.Entity<Tag>();
            modelBuilder.Entity<RecipeTag>()
                .HasKey(rt => new { rt.RecipeId, rt.TagId });
            modelBuilder.Entity<RecipeLike>()
                .HasKey(rl => new { rl.UserId, rl.RecipeId });
            modelBuilder.Entity<RecipeSave>()
                .HasKey(rs => new { rs.UserId, rs.RecipeId });

            modelBuilder.Ignore<IngredientAlias>();
            modelBuilder.Ignore<IngredientUnit>();
            modelBuilder.Entity<IngredientTag>()
                .HasKey(it => new { it.IngredientId, it.TagId });
            modelBuilder.Ignore<InventoryItem>();
            modelBuilder.Ignore<InventoryItemTag>();
            modelBuilder.Ignore<RecipeComment>();
            modelBuilder.Ignore<RecipeIngredientTag>();
            modelBuilder.Ignore<ChecklistItem>();
            modelBuilder.Ignore<KnowledgebaseArticle>();
            modelBuilder.Ignore<HouseholdInvitation>();
            modelBuilder.Ignore<UserPreference>();
            modelBuilder.Ignore<TagType>();
        }

        public override void Dispose()
        {
            base.Dispose();
            _connection.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
