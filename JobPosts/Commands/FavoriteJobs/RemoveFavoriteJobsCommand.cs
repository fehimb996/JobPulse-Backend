using MediatR;

namespace JobPosts.Commands.FavoriteJobs
{
    public record RemoveFavoriteJobsCommand(string UserId, List<int> Ids) : IRequest<Unit>;
}
