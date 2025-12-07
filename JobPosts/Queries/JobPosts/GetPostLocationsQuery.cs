using JobPosts.DTOs.JobPosts;
using MediatR;

namespace JobPosts.Queries.JobPosts
{
    public class GetPostLocationsQuery : IRequest<JobPostGroupedPagedResultDTO>
    {
        public string CountryCode { get; set; } = null!;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TimeframeInWeeks { get; set; } = 1;

        public string? ContractType { get; set; }
        public string? ContractTime { get; set; }
        public string? WorkLocation { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? Title { get; set; }
        public string? Location { get; set; }
        public string? Company { get; set; }
        public List<string>? Skills { get; set; }
        public List<string>? Languages { get; set; }
    }
}
