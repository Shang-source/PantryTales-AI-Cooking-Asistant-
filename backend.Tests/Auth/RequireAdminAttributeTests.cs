using System;
using System.Linq;
using backend.Auth;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace backend.Tests.Auth;

public class RequireAdminAttributeTests
{
    [Fact]
    public void RequireAdminAttribute_SetsRolesToAdmin()
    {
        var attribute = new RequireAdminAttribute();

        Assert.Equal("Admin", attribute.Roles);
    }

    [Fact]
    public void RequireAdminAttribute_IsAuthorizeAttribute()
    {
        var attribute = new RequireAdminAttribute();

        Assert.IsAssignableFrom<AuthorizeAttribute>(attribute);
    }

    [Fact]
    public void RequireAdminAttribute_CanBeAppliedToClass()
    {
        var attributeUsage = typeof(RequireAdminAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), true)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attributeUsage);
        Assert.True(attributeUsage.ValidOn.HasFlag(AttributeTargets.Class));
    }

    [Fact]
    public void RequireAdminAttribute_CanBeAppliedToMethod()
    {
        var attributeUsage = typeof(RequireAdminAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), true)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attributeUsage);
        Assert.True(attributeUsage.ValidOn.HasFlag(AttributeTargets.Method));
    }
}
