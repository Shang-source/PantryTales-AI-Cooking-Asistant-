using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Recipes;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class RecipeCookingControllerTests
{
    [Fact]
    public async Task GetAsync_ReturnsNotFound_WhenRecipeMissing()
    {
        var repo = new FakeRecipeRepository();
        var controller = new RecipeCookingController(repo, NullLogger<RecipeCookingController>.Instance);

        var result = await controller.GetAsync(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<CookingSessionDto>>(notFound.Value);
        Assert.Equal(404, payload.Code);
        Assert.Equal("Recipe not found.", payload.Message);
    }

    [Fact]
    public async Task GetAsync_ReturnsSteps_WhenRecipeHasStringArraySteps()
    {
        var recipeId = Guid.NewGuid();
        var repo = new FakeRecipeRepository
        {
            RecipeToReturn = NewRecipe(recipeId, """["Step one","Step two"]""")
        };
        var controller = new RecipeCookingController(repo, NullLogger<RecipeCookingController>.Instance);

        var result = await controller.GetAsync(recipeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<CookingSessionDto>>(ok.Value);
        Assert.Equal(0, payload.Code);
        var session = Assert.IsType<CookingSessionDto>(payload.Data);
        Assert.Equal(2, session.TotalSteps);
        Assert.Collection(session.Steps,
            step =>
            {
                Assert.Equal(1, step.Order);
                Assert.Equal("Step one", step.Instruction);
            },
            step =>
            {
                Assert.Equal(2, step.Order);
                Assert.Equal("Step two", step.Instruction);
            });
    }

    [Fact]
    public async Task GetAsync_OrdersStepsAndReadsDurations_WhenRecipeHasObjectSteps()
    {
        var recipeId = Guid.NewGuid();
        var repo = new FakeRecipeRepository
        {
            RecipeToReturn = NewRecipe(recipeId, """
            [
              { "order": 2, "instruction": "Second", "durationMinutes": 3 },
              { "order": 1, "instruction": "First", "durationSeconds": 30 }
            ]
            """)
        };
        var controller = new RecipeCookingController(repo, NullLogger<RecipeCookingController>.Instance);

        var result = await controller.GetAsync(recipeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var session = Assert.IsType<CookingSessionDto>(Assert.IsType<ApiResponse<CookingSessionDto>>(ok.Value).Data);
        Assert.Equal(2, session.Steps.Count);
        Assert.Equal(new[] { 1, 2 }, new[] { session.Steps[0].Order, session.Steps[1].Order });
        Assert.Equal(new[] { "First", "Second" }, new[] { session.Steps[0].Instruction, session.Steps[1].Instruction });
        Assert.Equal(30, session.Steps[0].SuggestedSeconds);
        Assert.Equal(180, session.Steps[1].SuggestedSeconds);
    }

    [Fact]
    public async Task GetAsync_ReturnsEmptySteps_OnMalformedJson()
    {
        var recipeId = Guid.NewGuid();
        var repo = new FakeRecipeRepository
        {
            RecipeToReturn = NewRecipe(recipeId, """{ "not": "an array" }""")
        };
        var controller = new RecipeCookingController(repo, NullLogger<RecipeCookingController>.Instance);

        var result = await controller.GetAsync(recipeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var session = Assert.IsType<CookingSessionDto>(Assert.IsType<ApiResponse<CookingSessionDto>>(ok.Value).Data);
        Assert.Empty(session.Steps);
        Assert.Equal(0, session.TotalSteps);
    }

    private static Recipe NewRecipe(Guid id, string stepsJson) =>
        new()
        {
            Id = id,
            HouseholdId = Guid.NewGuid(),
            Title = "Test Recipe",
            Steps = stepsJson
        };

    private sealed class FakeRecipeRepository : IRecipeRepository
    {
        public Recipe? RecipeToReturn { get; set; }

        public Task<Recipe?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.FromResult(RecipeToReturn is not null && RecipeToReturn.Id == recipeId
                ? RecipeToReturn
                : null);

        public Task<Recipe?> GetDetailedByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => GetByIdAsync(recipeId, cancellationToken);

        public Task<List<Recipe>> GetFeaturedRecipesAsync(int count, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Recipe>());

        public Task ToggleFeaturedAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
