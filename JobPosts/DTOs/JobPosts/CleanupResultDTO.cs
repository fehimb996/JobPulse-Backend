namespace JobPosts.DTOs.JobPosts
{
    public class CleanupResultDTO
    {
        public int DeletedCount { get; set; }
        public DateTime CutoffDate { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
