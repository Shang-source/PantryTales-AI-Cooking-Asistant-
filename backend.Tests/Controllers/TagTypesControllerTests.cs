using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers.Admin;
using backend.Data;
using backend.Dtos.Tags;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests.Controllers;

public class TagTypesControllerTests
{
    [Fact]
    public async Task ListAsync_ReturnsOrderedDtos()
    {
        await using var context = CreateContext();
        context.TagTypes.AddRange(
            new TagType { Name = "meal", DisplayName = "Meals" },
            new TagType { Name = "diet", DisplayName = "Diets" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = await controller.ListAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<TagTypeResponseDto>>(ok.Value);
        Assert.Equal(2, dtos.Count);
        Assert.Equal("Diets", dtos[0].DisplayName);
        Assert.Equal("Meals", dtos[1].DisplayName);
    }

    [Fact]
    public async Task GetAsync_ReturnsDto_WhenFound()
    {
        await using var context = CreateContext();
        var tagType = new TagType { Name = "meal", DisplayName = "Meals" };
        context.TagTypes.Add(tagType);
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = await controller.GetAsync(tagType.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TagTypeResponseDto>(ok.Value);
        Assert.Equal(tagType.Id, dto.Id);
        Assert.Equal("meal", dto.Name);
        Assert.Equal("Meals", dto.DisplayName);
    }

    [Fact]
    public async Task GetAsync_ReturnsNotFound_WhenMissing()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.GetAsync(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidationProblem_WhenNameEmpty()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var request = new CreateTagTypeRequestDto { Name = "   ", DisplayName = "Meals" };

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(problem.Value);
        Assert.True(!controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(request.Name)));
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidationProblem_WhenDisplayNameEmpty()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var request = new CreateTagTypeRequestDto { Name = "meal", DisplayName = "  " };

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(problem.Value);
        Assert.True(!controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(request.DisplayName)));
    }

    [Fact]
    public async Task CreateAsync_ReturnsConflict_WhenDuplicateExists()
    {
        await using var context = CreateContext();
        context.TagTypes.Add(new TagType { Name = "meal", DisplayName = "Meals" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);
        var request = new CreateTagTypeRequestDto { Name = " Meal ", DisplayName = "Meals" };

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var message = Assert.IsType<string>(conflict.Value);
        Assert.Contains("meal", message);
    }

    [Fact]
    public async Task CreateAsync_PersistsTagType()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var request = new CreateTagTypeRequestDto
        {
            Name = " Meal ",
            DisplayName = " Meals ",
            Description = "  desc "
        };

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<TagTypeResponseDto>(created.Value);
        Assert.Equal("meal", dto.Name);
        Assert.Equal("Meals", dto.DisplayName);
        Assert.Equal("desc", dto.Description);

        var saved = await context.TagTypes.SingleAsync();
        Assert.Equal(dto.Id, saved.Id);
        Assert.Equal("meal", saved.Name);
        Assert.Equal("Meals", saved.DisplayName);
        Assert.Equal("desc", saved.Description);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenMissing()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var request = new UpdateTagTypeRequestDto { Name = "meal", DisplayName = "Meals" };

        var result = await controller.UpdateAsync(123, request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsValidationProblem_WhenNameEmpty()
    {
        await using var context = CreateContext();
        context.TagTypes.Add(new TagType { Name = "meal", DisplayName = "Meals" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);
        var request = new UpdateTagTypeRequestDto { Name = "   ", DisplayName = "Meals" };

        var result = await controller.UpdateAsync(1, request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(problem.Value);
        Assert.True(!controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(request.Name)));
    }

    [Fact]
    public async Task UpdateAsync_ReturnsValidationProblem_WhenDisplayNameEmpty()
    {
        await using var context = CreateContext();
        context.TagTypes.Add(new TagType { Name = "meal", DisplayName = "Meals" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);
        var request = new UpdateTagTypeRequestDto { Name = "meal", DisplayName = "   " };

        var result = await controller.UpdateAsync(1, request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(problem.Value);
        Assert.True(!controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(request.DisplayName)));
    }

    [Fact]
    public async Task UpdateAsync_ReturnsConflict_WhenDuplicateExists()
    {
        await using var context = CreateContext();
        context.TagTypes.AddRange(
            new TagType { Name = "meal", DisplayName = "Meals" },
            new TagType { Name = "diet", DisplayName = "Diets" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);
        var request = new UpdateTagTypeRequestDto { Name = " meal ", DisplayName = "Meals" };

        var result = await controller.UpdateAsync(2, request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var message = Assert.IsType<string>(conflict.Value);
        Assert.Contains("meal", message);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesTagTypeAndAssociatedTags()
    {
        await using var context = CreateContext();
        context.TagTypes.AddRange(
            new TagType { Id = 1, Name = "meal", DisplayName = "Meals" },
            new TagType { Id = 2, Name = "diet", DisplayName = "Diets" });
        context.Tags.AddRange(
            new Tag { Name = "vegan", DisplayName = "Vegan", Type = "diet" },
            new Tag { Name = "salad", DisplayName = "Salad", Type = "meal" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);
        var request = new UpdateTagTypeRequestDto
        {
            Name = " lifestyle ",
            DisplayName = " Lifestyle ",
            Description = "  healthy "
        };

        var result = await controller.UpdateAsync(2, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TagTypeResponseDto>(ok.Value);
        Assert.Equal(2, dto.Id);
        Assert.Equal("lifestyle", dto.Name);
        Assert.Equal("Lifestyle", dto.DisplayName);
        Assert.Equal("healthy", dto.Description);

        var updatedTagType = await context.TagTypes.FindAsync(2);
        Assert.NotNull(updatedTagType);
        Assert.Equal("lifestyle", updatedTagType!.Name);
        Assert.Equal("Lifestyle", updatedTagType.DisplayName);
        Assert.Equal("healthy", updatedTagType.Description);

        context.ChangeTracker.Clear();
        var tags = await context.Tags.AsNoTracking().ToListAsync();
        Assert.Equal("lifestyle", tags.Single(t => t.Name == "vegan").Type);
        Assert.Equal("meal", tags.Single(t => t.Name == "salad").Type);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFound_WhenMissing()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.DeleteAsync(10, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsConflict_WhenTagsExist()
    {
        await using var context = CreateContext();
        context.TagTypes.Add(new TagType { Id = 1, Name = "meal", DisplayName = "Meals" });
        context.Tags.Add(new Tag { Name = "salad", DisplayName = "Salad", Type = "meal" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = await controller.DeleteAsync(1, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("Cannot delete tag type", Assert.IsType<string>(conflict.Value));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTagType_WhenNoTags()
    {
        await using var context = CreateContext();
        context.TagTypes.Add(new TagType { Id = 1, Name = "meal", DisplayName = "Meals" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = await controller.DeleteAsync(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(context.TagTypes);
    }

    private static TagTypesController CreateController(AppDbContext context) =>
        new(context);

    private static TestTagTypesDbContext CreateContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TestTagTypesDbContext(options, connection);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class TestTagTypesDbContext : AppDbContext
    {
        private readonly SqliteConnection _connection;

        public TestTagTypesDbContext(DbContextOptions<AppDbContext> options, SqliteConnection connection) : base(options)
        {
            _connection = connection;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TagType>();
            modelBuilder.Entity<Tag>();

            modelBuilder.Ignore<User>();
            modelBuilder.Ignore<Ingredient>();
            modelBuilder.Ignore<IngredientAlias>();
            modelBuilder.Ignore<IngredientUnit>();
            modelBuilder.Ignore<IngredientTag>();
            modelBuilder.Ignore<InventoryItem>();
            modelBuilder.Ignore<InventoryItemTag>();
            modelBuilder.Ignore<Recipe>();
            modelBuilder.Ignore<RecipeIngredient>();
            modelBuilder.Ignore<RecipeIngredientTag>();
            modelBuilder.Ignore<RecipeTag>();
            modelBuilder.Ignore<RecipeLike>();
            modelBuilder.Ignore<RecipeSave>();
            modelBuilder.Ignore<RecipeComment>();
            modelBuilder.Ignore<ChecklistItem>();
            modelBuilder.Ignore<KnowledgebaseArticle>();
            modelBuilder.Ignore<Household>();
            modelBuilder.Ignore<HouseholdMember>();
            modelBuilder.Ignore<HouseholdInvitation>();
            modelBuilder.Ignore<UserPreference>();
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
