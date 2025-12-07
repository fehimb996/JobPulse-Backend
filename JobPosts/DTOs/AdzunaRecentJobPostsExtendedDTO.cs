namespace JobPosts.DTOs
{
    public class AdzunaRecentJobPostsExtendedDTO
    {
        public List<AdzunaRecentJobPostDTO> Posts { get; set; } = new();
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }

        public List<AdzunaContractTypeCountDTO> ContractTypeCounts { get; set; } = new();
        public List<AdzunaContractTimeCountDTO> ContractTimeCounts { get; set; } = new();
        public List<AdzunaWorkLocationCountDTO> WorkLocationCounts { get; set; } = new();
        public List<AdzunaCompanyJobCountDTO> CompanyCounts { get; set; } = new();
        public List<AdzunaLocationJobCountDTO> LocationCounts { get; set; } = new();
    }
}
