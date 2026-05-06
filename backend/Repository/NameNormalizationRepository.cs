using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class NameNormalizationRepository(AppDbContext context) : INameNormalizationRepository
{
    public async Task<List<NameNormalizationToken>> GetTokensAsync(
        NameNormalizationTokenCategory? category = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.NameNormalizationTokens.AsQueryable();

        if (category.HasValue)
            query = query.Where(t => t.Category == category.Value);

        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        return await query
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Token)
            .ToListAsync(cancellationToken);
    }

    public async Task<NameNormalizationToken?> GetTokenByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await context.NameNormalizationTokens.FindAsync([id], cancellationToken);
    }

    public async Task<bool> TokenExistsAsync(string token, CancellationToken cancellationToken = default)
    {
        var normalizedToken = token.Trim().ToLowerInvariant();
        return await context.NameNormalizationTokens
            .AnyAsync(t => t.Token.ToLower() == normalizedToken, cancellationToken);
    }

    public async Task<NameNormalizationToken> AddTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default)
    {
        context.NameNormalizationTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task UpdateTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default)
    {
        token.UpdatedAt = DateTime.UtcNow;
        context.NameNormalizationTokens.Update(token);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default)
    {
        context.NameNormalizationTokens.Remove(token);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(int Created, int Skipped)> AddTokensAsync(
        IEnumerable<NameNormalizationToken> tokens,
        CancellationToken cancellationToken = default)
    {
        var tokenList = tokens.ToList();
        if (tokenList.Count == 0)
            return (0, 0);

        // Get existing tokens to skip duplicates
        var existingTokens = await context.NameNormalizationTokens
            .Select(t => t.Token.ToLower())
            .ToHashSetAsync(cancellationToken);

        var toAdd = new List<NameNormalizationToken>();
        var skipped = 0;

        foreach (var token in tokenList)
        {
            var normalizedToken = token.Token.Trim().ToLowerInvariant();
            if (existingTokens.Contains(normalizedToken))
            {
                skipped++;
                continue;
            }

            existingTokens.Add(normalizedToken); // Prevent duplicates within the batch
            toAdd.Add(token);
        }

        if (toAdd.Count > 0)
        {
            context.NameNormalizationTokens.AddRange(toAdd);
            await context.SaveChangesAsync(cancellationToken);
        }

        return (toAdd.Count, skipped);
    }

    public async Task<NameNormalizationDictionaryVersion> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var version = await context.NameNormalizationDictionaryVersions
            .FirstOrDefaultAsync(v => v.Id == 1, cancellationToken);

        if (version is null)
        {
            // Initialize if not exists
            version = new NameNormalizationDictionaryVersion { Id = 1 };
            context.NameNormalizationDictionaryVersions.Add(version);
            await context.SaveChangesAsync(cancellationToken);
        }

        return version;
    }

    public async Task IncrementDictionaryVersionAsync(CancellationToken cancellationToken = default)
    {
        var version = await GetVersionAsync(cancellationToken);
        version.DictionaryVersion++;
        version.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }
}
