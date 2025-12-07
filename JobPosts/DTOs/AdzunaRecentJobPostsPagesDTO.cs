namespace JobPosts.DTOs
{
    public class AdzunaRecentJobPostsPagesDTO
    {
        public List<AdzunaRecentJobPostDTO> Posts { get; set; } = new();
        public int TotalPages { get; set; }
    }
}
