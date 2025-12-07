using JobPosts.Queries.JobPosts;

namespace JobPosts.DTOs.JobPosts
{
    public class JobPostGroupedPagedResultDTO
    {
        public List<GroupedJobPostByLocationDTO> GroupedPosts { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
