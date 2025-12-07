namespace JobPosts.DTOs.JobPosts
{
    public class JobPostStatsDTO
    {
        public int TotalJobs { get; set; }
        public int TotalCompanies { get; set; }
        public int TotalLocations { get; set; }
        public int TotalWorkplaceModels { get; set; }
        public int TotalSkills { get; set; }
        public int TotalLanguages { get; set; }
        public int TimeframeInWeeks { get; set; }

        public int JobsWithCompany { get; set; }
        public int JobsWithLocation { get; set; }
        public int JobsWithWorkplaceModel { get; set; }
        public int JobsWithSkills { get; set; }
        public int JobsWithLanguages { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}
