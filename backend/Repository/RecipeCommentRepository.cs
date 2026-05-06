using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class RecipeCommentRepository(
    AppDbContext dbContext,
    ILogger<RecipeCommentRepository> logger) : IRecipeCommentRepository
{
    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> RecipeExistsAsync(Guid recipeId, CancellationToken cancellationToken = default)
        => await dbContext.Recipes
            .AsNoTracking()
            .AnyAsync(r => r.Id == recipeId, cancellationToken);

    public async Task<int> CountByRecipeIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
        => await dbContext.RecipeComments
            .AsNoTracking()
            .Where(comment => comment.RecipeId == recipeId)
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<(RecipeComment Comment, string AuthorNickname, string? AuthorAvatarUrl, int LikeCount, bool IsLikedByCurrentUser)>> ListByRecipeIdAsync(
        Guid recipeId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        // Step 1: load comments + author info (left join) as scalar projections.
        // We intentionally read required columns as nullable and filter out NULLs to avoid EF materialization errors
        // when legacy rows contain unexpected NULL values.
        var commentRows = await (from comment in dbContext.RecipeComments.AsNoTracking()
                                 let commentId = EF.Property<Guid?>(comment, nameof(RecipeComment.Id))
                                 let commentUserId = EF.Property<Guid?>(comment, nameof(RecipeComment.UserId))
                                 let commentRecipeId = EF.Property<Guid?>(comment, nameof(RecipeComment.RecipeId))
                                 let commentContent = EF.Property<string?>(comment, nameof(RecipeComment.Content))
                                 let commentCreatedAt = EF.Property<DateTime?>(comment, nameof(RecipeComment.CreatedAt))
                                 where commentRecipeId == recipeId
                                       && commentId != null
                                       && commentUserId != null
                                       && commentContent != null
                                       && commentCreatedAt != null
                                 join author in dbContext.Users.AsNoTracking()
                                     on commentUserId.Value equals author.Id into authors
                                 from author in authors.DefaultIfEmpty()
                                 orderby commentCreatedAt.Value descending
                                 select new
                                 {
                                     CommentId = commentId.Value,
                                     UserId = commentUserId.Value,
                                     RecipeId = commentRecipeId.Value,
                                     Content = commentContent!,
                                     CreatedAt = commentCreatedAt.Value,
                                     AuthorNickname = author != null ? author.Nickname : "Deleted user",
                                     AuthorAvatarUrl = author != null ? author.AvatarUrl : null
                                 })
            .ToListAsync(cancellationToken);

        if (commentRows.Count == 0)
        {
            return [];
        }

        var commentIds = commentRows.Select(r => r.CommentId).ToList();

        // Step 2: aggregate like counts for these comments.
        var likeCountByCommentId = await dbContext.CommentLikes
            .AsNoTracking()
            .Where(cl => commentIds.Contains(cl.CommentId))
            .GroupBy(cl => cl.CommentId)
            .Select(g => new { CommentId = g.Key, LikeCount = g.Count() })
            .ToDictionaryAsync(x => x.CommentId, x => x.LikeCount, cancellationToken);

        // Step 3: load current user's likes for these comments (optional).
        HashSet<Guid> likedCommentIds = [];
        if (currentUserId.HasValue)
        {
            var userId = currentUserId.Value;
            likedCommentIds = await dbContext.CommentLikes
                .AsNoTracking()
                .Where(cl => cl.UserId == userId && commentIds.Contains(cl.CommentId))
                .Select(cl => cl.CommentId)
                .ToHashSetAsync(cancellationToken);
        }

        // Step 4: merge in memory and return the same tuple shape.
        return commentRows
            .Select(row =>
            {
                var likeCount = likeCountByCommentId.TryGetValue(row.CommentId, out var count) ? count : 0;
                var isLiked = likedCommentIds.Contains(row.CommentId);

                return new ValueTuple<RecipeComment, string, string?, int, bool>(
                    new RecipeComment
                    {
                        Id = row.CommentId,
                        UserId = row.UserId,
                        RecipeId = row.RecipeId,
                        Content = row.Content,
                        CreatedAt = row.CreatedAt
                    },
                    row.AuthorNickname,
                    row.AuthorAvatarUrl,
                    likeCount,
                    isLiked);
            })
            .ToList();
    }

    public async Task<RecipeComment> AddAsync(Guid recipeId, Guid userId, string content, DateTime now,
        CancellationToken cancellationToken = default)
    {
        var comment = new RecipeComment
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            RecipeId = recipeId,
            Content = content,
            CreatedAt = now
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            dbContext.RecipeComments.Add(comment);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE recipes SET comments_count = comments_count + 1, updated_at = {now} WHERE id = {recipeId}",
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create comment for recipe {RecipeId} by user {UserId}", recipeId, userId);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        logger.LogInformation("Created comment {CommentId} for recipe {RecipeId} by user {UserId}", comment.Id,
            recipeId, userId);

        return comment;
    }

    public async Task<RecipeComment?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken = default)
        => await dbContext.RecipeComments.SingleOrDefaultAsync(c => c.Id == commentId, cancellationToken);

    public async Task DeleteAsync(RecipeComment comment, DateTime now, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            dbContext.RecipeComments.Remove(comment);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE recipes SET comments_count = GREATEST(comments_count - 1, 0), updated_at = {now} WHERE id = {comment.RecipeId}",
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete comment {CommentId} for recipe {RecipeId}", comment.Id,
                comment.RecipeId);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        logger.LogInformation("Deleted comment {CommentId} for recipe {RecipeId}", comment.Id, comment.RecipeId);
    }

    // Like-related methods

    public async Task<CommentLike?> GetLikeAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default)
        => await dbContext.CommentLikes
            .SingleOrDefaultAsync(cl => cl.CommentId == commentId && cl.UserId == userId, cancellationToken);

    public async Task<int> GetLikeCountAsync(Guid commentId, CancellationToken cancellationToken = default)
        => await dbContext.CommentLikes
            .AsNoTracking()
            .CountAsync(cl => cl.CommentId == commentId, cancellationToken);

    public async Task<CommentLike> AddLikeAsync(Guid commentId, Guid userId, DateTime now, CancellationToken cancellationToken = default)
    {
        var tracked = dbContext.CommentLikes.Local.SingleOrDefault(cl => cl.CommentId == commentId && cl.UserId == userId);
        if (tracked is not null)
        {
            return tracked;
        }

        var existing = await GetLikeAsync(commentId, userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var like = new CommentLike
        {
            CommentId = commentId,
            UserId = userId,
            CreatedAt = now
        };

        dbContext.CommentLikes.Add(like);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            dbContext.Entry(like).State = EntityState.Detached;

            var concurrentExisting = await GetLikeAsync(commentId, userId, cancellationToken);
            if (concurrentExisting is not null)
            {
                return concurrentExisting;
            }

            throw;
        }

        logger.LogInformation("User {UserId} liked comment {CommentId}", userId, commentId);

        return like;
    }

    public async Task<bool> RemoveLikeAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var deletedCount = await dbContext.CommentLikes
            .Where(cl => cl.CommentId == commentId && cl.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            logger.LogInformation("User {UserId} unliked comment {CommentId}", userId, commentId);
            return true;
        }

        return false;
    }
}
