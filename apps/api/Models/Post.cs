using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Api.Models;

public class Post
{
    public int Id { get; set; }

    public required string Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public required string UserId { get; set; }
    public User? User { get; set; }

    public bool IsDeleted { get; set; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ParentId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Post? Parent { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ICollection<Post>? Replies { get; set; } = new List<Post>();

    [NotMapped]
    public int CommentsCount { get; set; }
}

public class CreatePostDto
{
    public required string Content { get; set; }
    public int? ParentId { get; set; }
}
