using Api.Data;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IPostService
{
    Task<IEnumerable<Post>> GetAllPostsAsync();
    Task<Post> GetPostByIdAsync(int id);
    Task<Post> CreatePostAsync(string content, string userId, int? parentId = null);
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
                Content = p.Content,
                CreatedAt = p.CreatedAt,
                UserId = p.UserId,
                User = p.User,
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
                Content = p.Content,
                CreatedAt = p.CreatedAt,
                UserId = p.UserId,
                User = p.User,
                CommentsCount = p.Replies != null ? p.Replies.Count() : 0,
                Replies = p.Replies != null ? p.Replies.Select(r => new Post
                {
                    Id = r.Id,
                    Content = r.Content,
                    CreatedAt = r.CreatedAt,
                    UserId = r.UserId,
                    User = r.User,
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
            var parentExists = await context.Posts.AnyAsync(p => p.Id == parentId.Value);
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
}
