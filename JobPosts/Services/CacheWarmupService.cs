using JobPosts.Commands.JobPosts;
using JobPosts.Data;
using JobPosts.DTOs.JobPosts;
using JobPosts.Providers;
using JobPosts.Queries.JobPosts;
using MediatR;

namespace JobPosts.Services
{
    public class CacheWarmupService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<CacheWarmupService> _logger;
        private readonly SemaphoreSlim _semaphore;

        public CacheWarmupService(IServiceScopeFactory serviceScopeFactory, ILogger<CacheWarmupService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _semaphore = new SemaphoreSlim(3, 3);
        }

        public async Task WarmupCaches()
        {
            _logger.LogInformation("Starting warmup for all caches");

            // Define all countries that need cache warmup
            string[] countries = { "DE", "GB", "US", "NL", "BE", "AT", "CH", "NO", "DK" };
            var tasks = new List<Task>();

            // Warm up global cache (empty string)
            tasks.Add(WarmupCountrySpecificCaches(""));

            // Warm up each country's cache
            foreach (var country in countries)
            {
                tasks.Add(WarmupCountrySpecificCaches(country));
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation("Completed warmup for all caches");
        }

        public async Task WarmupCountrySpecificCaches(string countryCode)
        {
            var timeframes = new[] { 1, 2, 3, 4 };
            var tasks = new List<Task>();

            // Keep empty string as-is for global cache, otherwise normalize to uppercase
            var displayCountry = string.IsNullOrEmpty(countryCode) ? "GLOBAL" : countryCode.ToUpper();

            foreach (var timeframe in timeframes)
            {
                tasks.Add(WarmupWithSemaphore(() => WarmupFilterOptions(countryCode, timeframe)));
                tasks.Add(WarmupWithSemaphore(() => WarmupJobCounts(countryCode, timeframe)));
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation("Cache warmup completed for country [{Country}]", displayCountry);
        }

        private async Task WarmupWithSemaphore(Func<Task> warmupTask)
        {
            await _semaphore.WaitAsync();
            try
            {
                await warmupTask();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task WarmupFilterOptions(string country, int timeframe)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                // Set longer timeout for large countries and global cache
                var largeCountries = new[] { "DE", "US", "GB" };
                var isLargeOrGlobal = string.IsNullOrEmpty(country) || largeCountries.Contains(country.ToUpper());
                var timeout = isLargeOrGlobal ? TimeSpan.FromMinutes(8) : TimeSpan.FromMinutes(3);

                using var cts = new CancellationTokenSource(timeout);
                await mediator.Send(new GetFilterOptionsQuery
                {
                    CountryCode = country, // Use country as-is (empty string for global)
                    TimeframeInWeeks = timeframe
                }, cts.Token);

                var displayCountry = string.IsNullOrEmpty(country) ? "GLOBAL" : country;
                _logger.LogDebug("Successfully warmed up filter options cache for [{Country}]/[{Timeframe}]", displayCountry, timeframe);
            }
            catch (OperationCanceledException)
            {
                var displayCountry = string.IsNullOrEmpty(country) ? "GLOBAL" : country;
                _logger.LogWarning("Cache warmup timed out for [{Country}]/[{Timeframe}] - cache will be built on first request", displayCountry, timeframe);
            }
            catch (Exception ex)
            {
                var displayCountry = string.IsNullOrEmpty(country) ? "GLOBAL" : country;
                _logger.LogWarning(ex, "Failed to warm up filter options cache for [{Country}]/[{Timeframe}]", displayCountry, timeframe);
            }
        }

        private async Task WarmupJobCounts(string country, int timeframe)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await mediator.Send(new GetJobPostsQuery
                {
                    CountryCode = country, // Use country as-is (empty string for global)
                    TimeframeInWeeks = timeframe,
                    Page = 1,
                    PageSize = 1
                });

                var displayCountry = string.IsNullOrEmpty(country) ? "GLOBAL" : country;
                _logger.LogDebug("Successfully warmed up job counts cache for [{Country}]/[{Timeframe}]", displayCountry, timeframe);
            }
            catch (Exception ex)
            {
                var displayCountry = string.IsNullOrEmpty(country) ? "GLOBAL" : country;
                _logger.LogWarning(ex, "Failed to warm up job counts cache for [{Country}]/[{Timeframe}]", displayCountry, timeframe);
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
}
