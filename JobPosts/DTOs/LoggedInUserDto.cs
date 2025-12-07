using JobPosts.DTOs.JobPosts;

namespace JobPosts.DTOs;

public class LoggedInUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string ProfileImageUrl { get; set; } = string.Empty;
    public List<JobPostDTO> Favorites { get; set; } = new();
}
