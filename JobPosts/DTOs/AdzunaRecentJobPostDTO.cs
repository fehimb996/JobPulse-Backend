namespace JobPosts.DTOs
{
    public class AdzunaRecentJobPostDTO
    {
        public long JobId { get; set; }
        public string? Country { get; set; }
        public string? Company { get; set; }
        public string? Title { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }
        public double? SalaryMin { get; set; }
        public double? SalaryMax { get; set; }
        public DateTime? Created { get; set; }
        public string? Url { get; set; }
        public string? ContractType { get; set; }
        public string? ContractTime { get; set; }
        public string? Summary { get; set; }
        public string? WorkLocation { get; set; }
    }
}
