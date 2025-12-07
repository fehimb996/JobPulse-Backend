namespace JobPosts.Models
{
    public class JobPostLanguage
    {
        public int JobPostId { get; set; }
        public JobPost JobPost { get; set; }

        public int LanguageId { get; set; }
        public Language Language { get; set; }
    }
}
