using backend.Models;

namespace backend.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default);
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
    Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
}
