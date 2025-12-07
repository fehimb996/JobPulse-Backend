using MediatR;

namespace JobPosts.Queries.FavoriteJobs
{
    public record IsJobFavoritedQuery(string UserId, int Id) : IRequest<bool>;
}
