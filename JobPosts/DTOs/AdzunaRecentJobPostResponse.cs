using JobPosts.DTOs;

public class AdzunaRecentJobPostResponse
{
    public List<AdzunaRecentJobPostDTO> Jobs { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}