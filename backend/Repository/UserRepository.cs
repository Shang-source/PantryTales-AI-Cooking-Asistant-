using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .Include(u => u.Preferences)
            .ThenInclude(p => p.Tag)
            .FirstOrDefaultAsync(user => user.ClerkUserId == clerkUserId, cancellationToken);
    }
    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await context.Users.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }
}
