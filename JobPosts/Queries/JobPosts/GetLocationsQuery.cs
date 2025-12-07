using MediatR;

namespace JobPosts.Queries.JobPosts
{
    public class GetLocationsQuery : IRequest<List<string>>
    {
        public string? CountryCode { get; set; }
        public int TimeframeInWeeks { get; set; } = 1;
    }
}
