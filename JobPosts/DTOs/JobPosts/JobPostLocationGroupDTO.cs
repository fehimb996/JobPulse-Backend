namespace JobPosts.DTOs.JobPosts
{
    public class JobPostLocationGroupDTO
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int JobCount { get; set; }
        public List<JobPostWithCoordinatesDTO> JobPosts { get; set; } = new();
    }
}
