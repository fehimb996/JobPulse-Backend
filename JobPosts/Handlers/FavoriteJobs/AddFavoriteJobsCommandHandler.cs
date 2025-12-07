using JobPosts.Commands.FavoriteJobs;
using JobPosts.Data;
using JobPosts.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.FavoriteJobs
{
    public class AddFavoriteJobsCommandHandler : IRequestHandler<AddFavoriteJobsCommand, Unit>
    {
        private readonly JobPostsDbContext _context;
        private readonly ILogger<AddFavoriteJobsCommandHandler> _logger;

        public AddFavoriteJobsCommandHandler(JobPostsDbContext context, ILogger<AddFavoriteJobsCommandHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Unit> Handle(AddFavoriteJobsCommand request, CancellationToken cancellationToken)
        {
            if (request.Ids == null || request.Ids.Count == 0)
                return Unit.Value;

            // Sanitize and dedupe incoming Ids (use HashSet for O(1) lookups)
            var requestedIds = request.Ids.Where(id => id > 0).Distinct().ToList();
            if (!requestedIds.Any())
                return Unit.Value;

            // Lightweight user existence check
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == request.UserId, cancellationToken);

            if (!userExists)
                throw new UnauthorizedAccessException($"User '{request.UserId}' not found.");

            // Find which of the requested JobPost PKs are already favorited by this user
            var existingFavorites = await _context.UserFavoriteJobs
                .AsNoTracking()
                .Where(ufj => ufj.UserId == request.UserId && requestedIds.Contains(ufj.JobPostId))
                .Select(ufj => ufj.JobPostId)
                .ToListAsync(cancellationToken);

            var existingSet = new HashSet<int>(existingFavorites);
            var toAddIds = requestedIds.Where(id => !existingSet.Contains(id)).ToList();

            if (!toAddIds.Any())
                return Unit.Value; // nothing new

            // Validate that the JobPost IDs actually exist in JobPosts (optional but safer)
            var existingJobPostIds = await _context.JobPosts
                .AsNoTracking()
                .Where(j => toAddIds.Contains(j.Id))
                .Select(j => j.Id)
                .ToListAsync(cancellationToken);

            if (!existingJobPostIds.Any())
                return Unit.Value; // none of requested IDs correspond to real posts

            // Create join entities
            var newFavorites = existingJobPostIds
                .Select(id => new UserFavoriteJob { UserId = request.UserId, JobPostId = id })
                .ToList();

            // AddRange + Save. Handle possible race conditions (unique constraint) safely.
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Add in a single batch
                await _context.UserFavoriteJobs.AddRangeAsync(newFavorites, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);
                return Unit.Value;
            }
            catch (DbUpdateException dbEx)
            {
                // Likely a unique constraint violation due to concurrent insert — recover gracefully.
                _logger.LogWarning(dbEx, "DbUpdateException when adding favorites for user {UserId}. Will re-check which were inserted.", request.UserId);

                // Rollback and refresh which ones actually exist now.
                try
                {
                    await tx.RollbackAsync(cancellationToken);
                }
                catch { /* ignore rollback errors */ }

                var nowExisting = await _context.UserFavoriteJobs
                    .AsNoTracking()
                    .Where(ufj => ufj.UserId == request.UserId && toAddIds.Contains(ufj.JobPostId))
                    .Select(ufj => ufj.JobPostId)
                    .ToListAsync(cancellationToken);

                // If some didn't get inserted due to FK violation or other reason, you may want to log them:
                var inserted = nowExisting.Intersect(toAddIds).ToList();
                var failed = toAddIds.Except(nowExisting).ToList();

                if (failed.Any())
                {
                    _logger.LogWarning("Some favorite inserts failed for user {UserId}: {FailedIds}", request.UserId, string.Join(",", failed));
                    // You can decide whether to throw or silently ignore. We'll choose to throw a clear exception:
                    throw new InvalidOperationException($"Failed to add favorites for user {request.UserId}: {string.Join(',', failed)}");
                }

                // Otherwise everything is fine (concurrent insert succeeded); finish normally
                return Unit.Value;
            }
            catch (Exception ex)
            {
                try { await tx.RollbackAsync(cancellationToken); } catch { }
                _logger.LogError(ex, "Unexpected error while adding favorites for user {UserId}", request.UserId);
                throw;
            }
        }
    }
}
