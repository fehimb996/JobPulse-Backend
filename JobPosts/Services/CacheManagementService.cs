namespace JobPosts.Services
{
    public class CacheManagementService
    {
        private readonly CacheInvalidationService _invalidationService;
        private readonly CacheWarmupService _warmupService;
        private readonly ILogger<CacheManagementService> _logger;

        public CacheManagementService(
            CacheInvalidationService invalidationService,
            CacheWarmupService warmupService,
            ILogger<CacheManagementService> logger)
        {
            _invalidationService = invalidationService;
            _warmupService = warmupService;
            _logger = logger;
        }

        public async Task RefreshCacheForCountry(string countryCode)
        {
            _logger.LogInformation("\n\t\t-> Refreshing cache for country [{Country}]", countryCode);

            // Invalidate existing cache entries
            _invalidationService.InvalidateCountrySpecificCaches(countryCode);

            // Warm up new cache entries synchronously
            await _warmupService.WarmupCountrySpecificCaches(countryCode);

            // Update timestamp to track when cache was refreshed
            var cacheKey = string.IsNullOrWhiteSpace(countryCode) ? "country_GLOBAL" : $"country_{countryCode.ToUpper()}";
            _invalidationService.UpdateCacheTimestamp(cacheKey);

            _logger.LogInformation("\n\t\t-> Cache refresh completed for country [{Country}]", countryCode);
        }

        public async Task RefreshAllCaches()
        {
            _logger.LogInformation("\n\t\t-> Refreshing all caches");

            // Invalidate all cache entries
            _invalidationService.InvalidateJobPostCaches();

            // Warm up new cache entries synchronously
            await _warmupService.WarmupCaches();

            // Update timestamps for all countries
            string[] countries = { "DE", "GB", "US", "NL", "BE", "AT", "CH", "NO", "DK" };
            foreach (var country in countries)
            {
                _invalidationService.UpdateCacheTimestamp($"country_{country}");
            }
            _invalidationService.UpdateCacheTimestamp("country_GLOBAL");

            _logger.LogInformation("\n\t\t-> Complete cache refresh completed");
        }
    }
}
