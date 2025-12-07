namespace JobPosts.DTOs.JobPosts
{
    public class JobPostsWithCoordinatesResultDTO
    {
        public List<JobPostLocationGroupDTO> LocationGroups { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
