using Api.Data;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IPostService
{
    Task<IEnumerable<Post>> GetAllPostsAsync();
    Task<Post> GetPostByIdAsync(int id);
    Task<Post> CreatePostAsync(string content, string userId, int? parentId = null);
    Task<Post> UpdatePostAsync(int id, string content, string userId);
    Task DeletePostAsync(int id, string userId);
}

public class PostService(AppDbContext context) : IPostService
{
    public async Task<IEnumerable<Post>> GetAllPostsAsync()
    {
        return await context.Posts
            .AsNoTracking()
            .Where(p => p.ParentId == null)
            .Include(p => p.User)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new Post
            {
                Id = p.Id,
                Content = p.IsDeleted ? "[deleted]" : p.Content,
                CreatedAt = p.CreatedAt,
                UserId = p.UserId,
                User = p.IsDeleted ? null : p.User,
                IsDeleted = p.IsDeleted,
                CommentsCount = p.Replies != null ? p.Replies.Count() : 0,
                Replies = null
            })
            .ToListAsync();
    }

    public async Task<Post> GetPostByIdAsync(int id)
    {
        var post = await context.Posts
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new Post
            {
                Id = p.Id,
                Content = p.IsDeleted ? "[deleted]" : p.Content,
                CreatedAt = p.CreatedAt,
                UserId = p.UserId,
                User = p.IsDeleted ? null : p.User,
                IsDeleted = p.IsDeleted,
                ParentId = p.ParentId,
                Parent = p.Parent == null ? null : new Post
                {
                    Id = p.Parent.Id,
                    Content = p.Parent.IsDeleted ? "[deleted]" : p.Parent.Content,
                    CreatedAt = p.Parent.CreatedAt,
                    UserId = p.Parent.UserId,
                    User = p.Parent.IsDeleted ? null : p.Parent.User,
                    IsDeleted = p.Parent.IsDeleted,
                    CommentsCount = p.Parent.Replies != null ? p.Parent.Replies.Count() : 0,
                    ParentId = p.Parent.ParentId,
                    Replies = null
                },
                CommentsCount = p.Replies != null ? p.Replies.Count() : 0,
                Replies = p.Replies != null ? p.Replies.Select(r => new Post
                {
                    Id = r.Id,
                    Content = r.IsDeleted ? "[deleted]" : r.Content,
                    CreatedAt = r.CreatedAt,
                    UserId = r.UserId,
                    User = r.IsDeleted ? null : r.User,
                    IsDeleted = r.IsDeleted,
                    CommentsCount = r.Replies != null ? r.Replies.Count() : 0,
                    Replies = null
                }).ToList() : null
            })
            .FirstOrDefaultAsync();

        if (post == null)
        {
            throw new KeyNotFoundException($"Post with id {id} not found.");
        }

        return post;
    }

    public async Task<Post> CreatePostAsync(string content, string userId, int? parentId = null)
    {
        if (parentId.HasValue)
        {
            var parentExists = await context.Posts.AnyAsync(p => p.Id == parentId.Value && !p.IsDeleted);
            if (!parentExists)
            {
                throw new KeyNotFoundException($"Parent post with id {parentId.Value} not found.");
            }
        }

        var post = new Post
        {
            Content = content,
            UserId = userId,
            ParentId = parentId
        };

        context.Posts.Add(post);
        await context.SaveChangesAsync();

        return post;
    }

    public async Task<Post> UpdatePostAsync(int id, string content, string userId)
    {
        var post = await context.Posts.FirstOrDefaultAsync(p => p.Id == id);

        if (post == null)
        {
            throw new KeyNotFoundException($"Post with id {id} not found.");
        }

        if (post.IsDeleted)
        {
            throw new InvalidOperationException("Cannot edit a deleted post.");
        }

        if (post.UserId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to edit this post.");
        }

        post.Content = content;
        await context.SaveChangesAsync();

        return post;
    }

    public async Task DeletePostAsync(int id, string userId)
    {
        var post = await context.Posts
            .Include(p => p.Replies)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (post == null)
        {
            throw new KeyNotFoundException($"Post with id {id} not found.");
        }

        if (post.UserId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to delete this post.");
        }

        if (post.Replies != null && post.Replies.Any())
        {
            post.IsDeleted = true;
        }
        else
        {
            context.Posts.Remove(post);
        }

        await context.SaveChangesAsync();
    }
}
