using Hangfire;
using JobPosts.Commands.Careerjet;
using JobPosts.Services;
using MediatR;

namespace JobPosts.Hangfire
{
    public class CareerjetJobRunner
    {
        private readonly IMediator _mediator;
        private readonly BackgroundCacheWarmupService _backgroundCacheService;
        private readonly CacheInvalidationService _cacheInvalidationService;
        private readonly ILogger<CareerjetJobRunner> _logger;

        public CareerjetJobRunner(
            IMediator mediator,
            BackgroundCacheWarmupService backgroundCacheService,
            CacheInvalidationService cacheInvalidationService,
            ILogger<CareerjetJobRunner> logger)
        {
            _mediator = mediator;
            _backgroundCacheService = backgroundCacheService;
            _cacheInvalidationService = cacheInvalidationService;
            _logger = logger;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 3600)]
        public async Task RunAllCountriesAsync()
        {
            string[] countries = { "no", "dk" };
            var countriesWithUpdates = new List<string>();

            _logger.LogInformation("Starting Careerjet job fetch for all countries");

            foreach (var country in countries)
            {
                try
                {
                    _logger.LogInformation("Starting Careerjet fetch for country: [{Country}]", country.ToUpper());
                    var result = await _mediator.Send(new FetchCareerjetJobsCommand(country));

                    if (result.SavedJobs > 0)
                    {
                        countriesWithUpdates.Add(country.ToUpper());
                        _cacheInvalidationService.InvalidateCountrySpecificCaches(country.ToUpper());
                        _backgroundCacheService.QueueCacheWarmup(country.ToUpper());
                    }

                    _logger.LogInformation("Finished Careerjet [{Country}] - Inserted: [{SavedJobs}], Pages: [{TotalPages}]",
                        country.ToUpper(), result.SavedJobs, result.TotalPages);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching Careerjet data for country [{Country}]", country.ToUpper());
                }

                await Task.Delay(1000);
            }

            if (countriesWithUpdates.Any())
            {
                _cacheInvalidationService.InvalidateCountrySpecificCaches(""); // Global cache
                _backgroundCacheService.QueueCacheWarmup(""); // Queue global cache warmup

                _logger.LogInformation("Careerjet countries with cache warmup queued: [{Countries}] + GLOBAL",
                    string.Join(", ", countriesWithUpdates));
            }
            else
            {
                _logger.LogInformation("No Careerjet countries had new records - no cache operations performed");
            }
        }
    }
}
