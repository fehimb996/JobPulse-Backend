using JobPosts.Commands.FavoriteJobs;
using JobPosts.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.FavoriteJobs
{
    public class RemoveFavoriteJobsCommandHandler : IRequestHandler<RemoveFavoriteJobsCommand, Unit>
    {
        private readonly JobPostsDbContext _context;

        public RemoveFavoriteJobsCommandHandler(JobPostsDbContext context) => _context = context;

        public async Task<Unit> Handle(RemoveFavoriteJobsCommand request, CancellationToken cancellationToken)
        {
            // Fetch the join rows to remove
            var toRemove = await _context.UserFavoriteJobs
                .Where(ufj => ufj.UserId == request.UserId && request.Ids.Contains(ufj.JobPostId))
                .ToListAsync(cancellationToken);

            if (!toRemove.Any()) return Unit.Value;

            _context.UserFavoriteJobs.RemoveRange(toRemove);
            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
