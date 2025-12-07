namespace JobPosts.DTOs.JobPosts
{
    public class LookupIds
    {
        public int? CountryId { get; set; }
        public bool CountryNotFound { get; set; }
        public List<int>? CompanyIds { get; set; }
        public List<int>? LocationIds { get; set; }
        public int? ContractTypeId { get; set; }
        public int? ContractTimeId { get; set; }
        public int? WorkplaceModelId { get; set; }
        public List<int>? SkillIds { get; set; }
        public List<int>? LanguageIds { get; set; }

        public bool HasEmptyResults =>
            CountryNotFound ||
            (CompanyIds?.Count == 0) ||
            (LocationIds?.Count == 0) ||
            (SkillIds?.Count == 0) ||
            (LanguageIds?.Count == 0);
    }
}
