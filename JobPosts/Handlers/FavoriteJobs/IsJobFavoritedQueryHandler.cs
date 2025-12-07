using MediatR;
using Microsoft.EntityFrameworkCore;
using JobPosts.Data;
using JobPosts.Queries.FavoriteJobs;

namespace JobPosts.Handlers.FavoriteJobs
{
    public class IsJobFavoritedQueryHandler : IRequestHandler<IsJobFavoritedQuery, bool>
    {
        private readonly JobPostsDbContext _context;

        public IsJobFavoritedQueryHandler(JobPostsDbContext context) => _context = context;

        public async Task<bool> Handle(IsJobFavoritedQuery request, CancellationToken cancellationToken)
        {
            // Quick lookup on the join table (composite PK)
            return await _context.UserFavoriteJobs
                .AsNoTracking()
                .AnyAsync(ufj => ufj.UserId == request.UserId && ufj.JobPostId == request.Id, cancellationToken);
        }
    }
}
