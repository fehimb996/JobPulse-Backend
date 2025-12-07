using JobPosts.DTOs;
using JobPosts.DTOs.JobPosts;
using MediatR;

namespace JobPosts.Queries.JobPosts
{
    public class GetJobPostsWithCoordinatesQuery : IRequest<JobPostsWithCoordinatesResultDTO>
    {
        public string CountryCode { get; set; } = null!;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TimeframeInWeeks { get; set; } = 1;
        public string? ContractType { get; set; }
        public string? ContractTime { get; set; }
        public string? WorkLocation { get; set; }
        public string? Title { get; set; }
        public string? Location { get; set; }
        public string? Company { get; set; }
        public List<string>? Skills { get; set; }
        public List<string>? Languages { get; set; }

        // Optional: If you want to filter by specific location
        public int? LocationId { get; set; }

        // New property for grouping preference
        public bool GroupByLocation { get; set; } = true;

        // New property for summary mode (just location info + counts, no job details)
        public bool SummaryMode { get; set; } = false;

        // New property to get all results without pagination (useful for specific location)
        public bool GetAll { get; set; } = false;
    }
}
