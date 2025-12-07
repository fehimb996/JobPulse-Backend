namespace JobPosts.DTOs.JobPosts
{
    public class JobPostPagedResultDTO
    {
        public List<JobPostDTO> Posts { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public string? Message { get; set; }
        public bool HasResults => Posts.Any() && TotalCount > 0;
        public bool HasNextPage { get; set; }
    }
}
