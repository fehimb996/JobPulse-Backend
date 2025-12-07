namespace JobPosts.DTOs.JobPosts
{
    public class JobPostsByLocationDTO
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string Url { get; set; }
        public double? SalaryMin { get; set; }
        public double? SalaryMax { get; set; }
        public DateTime Created { get; set; }

        public string? CompanyName { get; set; }
        public string? LocationName { get; set; }
        public string? ContractType { get; set; }
        public string? ContractTime { get; set; }
        public string? WorkLocation { get; set; }

        public List<string> Skills { get; set; } = new();
        public List<string> Languages { get; set; } = new();
    }
}
