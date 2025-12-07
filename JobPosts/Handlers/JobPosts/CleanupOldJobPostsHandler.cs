using JobPosts.Commands.JobPosts;
using JobPosts.DTOs.JobPosts;
using MediatR;
using JobPosts.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.JobPosts
{
    public class CleanupOldJobPostsHandler : IRequestHandler<CleanupOldJobPostsCommand, CleanupResultDTO>
    {
        private readonly JobPostsDbContext _context;
        private readonly ILogger<CleanupOldJobPostsHandler> _logger;
        private const int DAYS_TO_KEEP = 30;
        private const int BATCH_SIZE = 1000; // Process in batches to avoid memory issues

        public CleanupOldJobPostsHandler(
            JobPostsDbContext context,
            ILogger<CleanupOldJobPostsHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CleanupResultDTO> Handle(
            CleanupOldJobPostsCommand request,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var cutoffDate = DateTime.UtcNow.AddDays(-DAYS_TO_KEEP);
            var totalDeleted = 0;

            _logger.LogInformation(
                "\n\t\t-> Starting cleanup of JobPosts older than {CutoffDate} ({DaysToKeep} days)",
                cutoffDate, DAYS_TO_KEEP);

            try
            {
                // Get count of records to be deleted for logging
                var totalToDelete = await _context.JobPosts
                    .Where(jp => jp.Created < cutoffDate)
                    .CountAsync(cancellationToken);

                _logger.LogInformation(
                    "\n\t\t-> Found {TotalToDelete} JobPosts to delete", totalToDelete);

                if (totalToDelete == 0)
                {
                    stopwatch.Stop();
                    _logger.LogInformation("\n\t\t-> No JobPosts to delete");
                    return new CleanupResultDTO
                    {
                        DeletedCount = 0,
                        CutoffDate = cutoffDate,
                        Duration = stopwatch.Elapsed
                    };
                }

                // Delete in batches to avoid performance issues
                var batchCount = 0;
                while (true)
                {
                    var batch = await _context.JobPosts
                        .Where(jp => jp.Created < cutoffDate)
                        .Take(BATCH_SIZE)
                        .ToListAsync(cancellationToken);

                    if (!batch.Any())
                        break;

                    _context.JobPosts.RemoveRange(batch);
                    var deletedInBatch = await _context.SaveChangesAsync(cancellationToken);

                    totalDeleted += deletedInBatch;
                    batchCount++;

                    _logger.LogDebug(
                        "\n\t\t-> Batch {BatchCount}: Deleted {DeletedInBatch} records. Total deleted: {TotalDeleted}",
                        batchCount, deletedInBatch, totalDeleted);

                    // Small delay between batches to reduce database load
                    await Task.Delay(100, cancellationToken);
                }

                stopwatch.Stop();

                _logger.LogInformation(
                    "\n\t\t-> Cleanup completed: Deleted {TotalDeleted} JobPosts in {Duration}ms ({Batches} batches)",
                    totalDeleted, stopwatch.ElapsedMilliseconds, batchCount);

                return new CleanupResultDTO
                {
                    DeletedCount = totalDeleted,
                    CutoffDate = cutoffDate,
                    Duration = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    "\n\t\t-> Error during JobPosts cleanup. Deleted {TotalDeleted} before error occurred",
                    totalDeleted);
                throw;
            }
        }
    }
}
