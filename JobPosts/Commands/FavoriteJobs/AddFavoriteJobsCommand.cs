using MediatR;

namespace JobPosts.Commands.FavoriteJobs
{
    public record AddFavoriteJobsCommand(string UserId, List<int> Ids) : IRequest<Unit>;
}
