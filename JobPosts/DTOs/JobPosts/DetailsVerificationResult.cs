namespace JobPosts.DTOs.JobPosts
{
    public class DetailsVerificationResult
    {
        public int TotalRows { get; set; }
        public int UrlMatchingDetails { get; set; }
        public int MarkedRows { get; set; }
        public int MarkedButNoMatch { get; set; }
        public int UnmarkedButMatch { get; set; }
    }
}
