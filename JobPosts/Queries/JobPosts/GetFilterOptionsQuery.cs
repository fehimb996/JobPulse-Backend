using MediatR;
using JobPosts.DTOs.JobPosts;

namespace JobPosts.Queries.JobPosts
{
    public class GetFilterOptionsQuery : IRequest<FilterOptionsDTO>
    {
        public string? CountryCode { get; set; }
        public int TimeframeInWeeks { get; set; } = 1;
    }
}
