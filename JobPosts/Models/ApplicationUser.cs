using Microsoft.AspNetCore.Identity;

namespace JobPosts.Models;

public class ApplicationUser : IdentityUser
{
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Surname { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = string.Empty;
    public string ProfileImageUrl { get; set; } = string.Empty;
    public bool IsGoogleAccount { get; set; } = false;
    public bool IsAppleAccount { get; set; } = false;
    public ICollection<UserFavoriteJob> FavoriteJobs { get; set; } = new List<UserFavoriteJob>();
}