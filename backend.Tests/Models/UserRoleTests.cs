using backend.Models;
using Xunit;

namespace backend.Tests.Models;

public class UserRoleTests
{
    [Fact]
    public void UserRole_HasCorrectValues()
    {
        Assert.Equal(0, (int)UserRole.User);
        Assert.Equal(1, (int)UserRole.Admin);
        Assert.Equal(2, (int)UserRole.Moderator);
    }

    [Fact]
    public void User_RoleDefaultsToUser()
    {
        var user = new User();

        Assert.Equal(UserRole.User, user.Role);
    }

    [Fact]
    public void User_CanSetRoleToAdmin()
    {
        var user = new User
        {
            Role = UserRole.Admin
        };

        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public void User_CanSetRoleToModerator()
    {
        var user = new User
        {
            Role = UserRole.Moderator
        };

        Assert.Equal(UserRole.Moderator, user.Role);
    }
}
