namespace JobPosts.DTOs.JobPosts
{
    public class FilterCountResult
    {
        public string Category { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
