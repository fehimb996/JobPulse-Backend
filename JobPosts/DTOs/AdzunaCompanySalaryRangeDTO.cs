namespace JobPosts.DTOs
{
    public class AdzunaCompanySalaryRangeDTO
    {
        public string? Company { get; set; }
        public string? Country { get; set; }
        public decimal? AvgSalaryMin { get; set; }
        public decimal? AvgSalaryMax { get; set; }
        public int JobCount { get; set; }
    }
}
