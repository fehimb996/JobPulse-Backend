using MediatR;

namespace JobPosts.Queries.JobPosts
{
    public class GetContractTypesQuery : IRequest<List<string>>
    {
        public string? CountryCode { get; set; }
        public int TimeframeInWeeks { get; set; } = 1;
    }
}
