using Hangfire;
using JobPosts.Commands.JobPosts;
using JobPosts.Services;
using MediatR;

namespace JobPosts.Hangfire
{
    public class AdzunaJobRunner
    {
        private readonly IMediator _mediator;
        private readonly BackgroundCacheWarmupService _backgroundCacheService;
        private readonly CacheInvalidationService _cacheInvalidationService;
        private readonly ILogger<AdzunaJobRunner> _logger;

        public AdzunaJobRunner(
            IMediator mediator,
            BackgroundCacheWarmupService backgroundCacheService,
            CacheInvalidationService cacheInvalidationService,
            ILogger<AdzunaJobRunner> logger)
        {
            _mediator = mediator;
            _backgroundCacheService = backgroundCacheService;
            _cacheInvalidationService = cacheInvalidationService;
            _logger = logger;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 3600)]
        public async Task RunAllCountriesAsync()
        {
            string[] countries = { "de", "gb", "us", "nl", "be", "at", "ch" };
            var countriesWithUpdates = new List<string>();

            _logger.LogInformation("\n\t\t\tStarting job fetch for all countries");

            foreach (var country in countries)
            {
                try
                {
                    _logger.LogInformation("\n\t\t\tStarting fetch for country: [{Country}]", country.ToUpper());
                    var result = await _mediator.Send(new FetchAdzunaJobsCommand(country));

                    if (result.SavedJobs > 0)
                    {
                        countriesWithUpdates.Add(country.ToUpper());
                        // Immediately invalidate cache for this country
                        _cacheInvalidationService.InvalidateCountrySpecificCaches(country.ToUpper());
                        // Queue background cache warmup
                        _backgroundCacheService.QueueCacheWarmup(country.ToUpper());
                    }
                    else
                    {
                        // Check if cache needs refresh due to time (8 hours)
                        var cacheKey = $"country_{country.ToUpper()}";
                        if (_cacheInvalidationService.ShouldRefreshCache(cacheKey, TimeSpan.FromHours(8)))
                        {
                            _logger.LogInformation("\n\t\t\tCache expired for [{Country}], queueing refresh", country.ToUpper());
                            _backgroundCacheService.QueueCacheWarmup(country.ToUpper());
                            _cacheInvalidationService.UpdateCacheTimestamp(cacheKey);
                        }
                    }

                    _logger.LogInformation("\n\t\tFinished [{Country}] - Inserted: [{SavedJobs}], Pages: [{TotalPages}]",
                        country.ToUpper(), result.SavedJobs, result.TotalPages);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "\n\t\tError fetching for country [{Country}]", country.ToUpper());
                }

                await Task.Delay(1000);
            }

            // Handle global cache only ONCE after all countries are processed
            if (countriesWithUpdates.Any())
            {
                _cacheInvalidationService.InvalidateCountrySpecificCaches(""); // Global cache
                _backgroundCacheService.QueueCacheWarmup(""); // Queue global cache warmup
                _logger.LogInformation("\n\t\t\tCountries with updates: [{Countries}] + GLOBAL cache invalidated",
                    string.Join(", ", countriesWithUpdates));
            }
            else
            {
                // Check if global cache needs refresh due to time
                var globalCacheKey = "country_GLOBAL";
                if (_cacheInvalidationService.ShouldRefreshCache(globalCacheKey, TimeSpan.FromHours(8)))
                {
                    _logger.LogInformation("\n\t\t\tGlobal cache expired, queueing refresh");
                    _backgroundCacheService.QueueCacheWarmup("");
                    _cacheInvalidationService.UpdateCacheTimestamp(globalCacheKey);
                }
                else
                {
                    _logger.LogInformation("\n\t\t\tNo countries had new records and global cache is still fresh");
                }
            }
        }
    }
}
