namespace JobPosts.DTOs.JobPosts
{
    public class JobPostWithCoordinatesDTO
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Url { get; set; } = string.Empty;
        public double? SalaryMin { get; set; }
        public double? SalaryMax { get; set; }

        // Hybrid coordinates - either from JobPost or Location
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        // Coordinate source info (helpful for debugging/frontend)
        public string CoordinateSource { get; set; } = string.Empty; // "JobPost" or "Location"

        public DateTime Created { get; set; }
        public string CountryName { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public string? CompanyName { get; set; }
        public string? LocationName { get; set; }
        public string? ContractType { get; set; }
        public string? ContractTime { get; set; }
        public string? WorkLocation { get; set; }
        public List<string> Skills { get; set; } = new();
        public List<string> Languages { get; set; } = new();
    }
}
