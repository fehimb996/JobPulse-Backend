namespace JobPosts.Models
{
    public class Language
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ICollection<JobPostLanguage> JobPostLanguages { get; set; } = new List<JobPostLanguage>();
    }
}
