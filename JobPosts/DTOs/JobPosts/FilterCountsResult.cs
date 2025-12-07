namespace JobPosts.DTOs.JobPosts
{
    public class FilterCountsResult
    {
        public List<FilterCountDTO> ContractTypeCounts { get; set; } = new();
        public List<FilterCountDTO> ContractTimeCounts { get; set; } = new();
        public List<FilterCountDTO> WorkLocationCounts { get; set; } = new();
        public List<FilterCountDTO> CompanyCounts { get; set; } = new();
        public List<FilterCountDTO> LocationCounts { get; set; } = new();
        public List<FilterCountDTO> SkillCounts { get; set; } = new();
        public List<FilterCountDTO> LanguageCounts { get; set; } = new();
        public List<FilterCountDTO> CountryCounts { get; set; } = new();
    }
}
