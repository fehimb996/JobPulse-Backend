using JobPosts.DTOs.JobPosts;
using MediatR;

namespace JobPosts.Queries.JobPosts
{
    public class GetPostsByLocationIdQuery : IRequest<PostsByLocationPagedResultDTO>
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TimeframeInWeeks { get; set; } = 1;

        public string? ContractType { get; set; }
        public string? ContractTime { get; set; }
        public string? WorkLocation { get; set; }
        public string? Title { get; set; }
        public string? Company { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public List<string>? Skills { get; set; }
        public List<string>? Languages { get; set; }

        public string? UserId { get; set; }
        public int LocationId { get; set; } 
    }
}
