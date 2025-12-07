using JobPosts.Data;
using JobPosts.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.JobPosts
{
    public abstract class BaseFilterQueryHandler<TQuery, TResult>
    {
        protected readonly IServiceProvider _serviceProvider;
        protected readonly IMemoryCache _cache;
        protected readonly CacheInvalidationService _cacheInvalidationService;
        protected readonly ILogger _logger;

        protected BaseFilterQueryHandler(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            CacheInvalidationService cacheInvalidationService,
            ILogger logger)
        {
            _serviceProvider = serviceProvider;
            _cache = cache;
            _cacheInvalidationService = cacheInvalidationService;
            _logger = logger;
        }

        protected (DateTime fromDate, DateTime toDate) CalculateDateRange(int timeframeInWeeks)
        {
            DateTime fromDate, toDate;
            if (timeframeInWeeks == 1)
            {
                fromDate = DateTime.UtcNow.AddDays(-7);
                toDate = DateTime.UtcNow;
            }
            else
            {
                var weeksAgo = timeframeInWeeks - 1;
                toDate = DateTime.UtcNow.AddDays(-7 * weeksAgo);
                fromDate = toDate.AddDays(-7);
            }
            return (fromDate, toDate);
        }

        protected string GenerateCacheKey(string prefix, string? countryCode, int timeframeInWeeks, DateTime fromDate, DateTime toDate)
        {
            var normalizedCountryCode = string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim();
            var countryKey = normalizedCountryCode ?? "ALL";
            return $"{prefix}:{countryKey}:{timeframeInWeeks}:{fromDate:yyyyMMddHH}:{toDate:yyyyMMddHH}";
        }

        protected MemoryCacheEntryOptions CreateCacheOptions()
        {
            return new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(8),
                Priority = CacheItemPriority.High,
                PostEvictionCallbacks = { new PostEvictionCallbackRegistration
                {
                    EvictionCallback = (key, value, reason, state) =>
                    {
                        if (state is CacheInvalidationService invalidationService && key != null)
                        {
                            invalidationService.RemoveCacheKey(key.ToString());
                        }
                    },
                    State = _cacheInvalidationService
                }}
            };
        }

        protected void SetupDatabaseTimeout(JobPostsDbContext context, out int? previousTimeout)
        {
            const int timeoutSeconds = 30;
            previousTimeout = context.Database.GetCommandTimeout();
            context.Database.SetCommandTimeout(timeoutSeconds);
        }

        protected void RestoreDatabaseTimeout(JobPostsDbContext context, int? previousTimeout)
        {
            context.Database.SetCommandTimeout(previousTimeout);
        }
    }
}
