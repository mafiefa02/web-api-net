using System.Text.Json.Serialization;

namespace Api.Models;

public class User
{
    public string Id { get; set; } = String.Empty;
    public string Username { get; set; } = string.Empty;
    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;
    [JsonIgnore]
    public string? RefreshToken { get; set; }
    [JsonIgnore]
    public DateTime? RefreshTokenExpiryTime { get; set; }
}
