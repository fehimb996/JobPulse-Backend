using Hangfire;
using JobPosts.Commands.JobPosts;
using MediatR;

namespace JobPosts.Hangfire
{
    public class JobPostCleanupRunner
    {
        private readonly IMediator _mediator;
        private readonly ILogger<JobPostCleanupRunner> _logger;

        public JobPostCleanupRunner(
            IMediator mediator,
            ILogger<JobPostCleanupRunner> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 3600)] // 1 hour timeout
        public async Task RunCleanupAsync()
        {
            _logger.LogInformation("\n\t\t\tStarting JobPost cleanup job");

            try
            {
                var result = await _mediator.Send(new CleanupOldJobPostsCommand());

                _logger.LogInformation(
                    "\n\t\t\tJobPost cleanup completed - Deleted: {DeletedCount}, Duration: {Duration}",
                    result.DeletedCount, result.Duration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "\n\t\t\tError during JobPost cleanup");
                throw;
            }
        }
    }
}
