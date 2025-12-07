namespace JobPosts.DTOs.JobPosts
{
    public class PostsByLocationPagedResultDTO
    {
        public List<JobPostsByLocationDTO> Posts { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
