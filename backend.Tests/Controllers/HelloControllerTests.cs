using System.Security.Claims;
using backend.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace backend.Tests.Controllers;

public class HelloControllerTests
{
    [Fact]
    public void Get_ReturnsHelloWorld()
    {
        var controller = CreateController(BuildPrincipal("clerk_123"));

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        var value = ok.Value;
        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        Assert.Equal("Hello, World!", messageProperty.GetValue(value));
    }

    private static HelloController CreateController(ClaimsPrincipal user) =>
        new()
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

    private static ClaimsPrincipal BuildPrincipal(string clerkUserId)
    {
        var identity = new ClaimsIdentity([new Claim("clerk_user_id", clerkUserId)], "mock");
        return new ClaimsPrincipal(identity);
    }
}
