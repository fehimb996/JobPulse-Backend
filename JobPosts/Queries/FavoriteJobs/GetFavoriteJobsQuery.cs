using JobPosts.DTOs;
using JobPosts.DTOs.JobPosts;
using MediatR;

namespace JobPosts.Queries.FavoriteJobs
{
    public record GetFavoriteJobsQuery(string UserId) : IRequest<List<JobPostDTO>>;
}
