using Microsoft.Extensions.Caching.Memory;
using System.Collections;
using System.Reflection;

namespace JobPosts.Services
{
    public class CacheInvalidationService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheInvalidationService> _logger;
        private readonly ConcurrentHashSet<string> _cacheKeys;

        public CacheInvalidationService(IMemoryCache cache, ILogger<CacheInvalidationService> logger)
        {
            _cache = cache;
            _logger = logger;
            _cacheKeys = new ConcurrentHashSet<string>();
        }

        public void TrackCacheKey(string key)
        {
            _cacheKeys.Add(key);
        }

        public void RemoveCacheKey(string key)
        {
            _cacheKeys.TryRemove(key);
        }

        public void InvalidateCountrySpecificCaches(string countryCode)
        {
            var normalizedCountryCode = string.IsNullOrWhiteSpace(countryCode) ? "ALL" : countryCode.ToUpper();

            // Improved pattern matching for your cache key format
            var keysToRemove = _cacheKeys.Where(key =>
                key.Contains($"country:{normalizedCountryCode}") ||     // Matches "country:DE" pattern
                key.Contains($"country:ALL") && string.IsNullOrWhiteSpace(countryCode) || // Global cache
                key.StartsWith($"filter_options:{normalizedCountryCode}") ||
                key.StartsWith($"job_posts_{normalizedCountryCode}_") ||
                key.StartsWith($"total_count_") && key.Contains($"country:{normalizedCountryCode}"))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
                _cacheKeys.TryRemove(key);
            }

            _logger.LogInformation("\n\t\t-> Invalidated [{Count}] cache entries for country [{Country}]",
                keysToRemove.Count, normalizedCountryCode);
        }

        // New method to check if cache needs refresh based on time
        public bool ShouldRefreshCache(string cacheKey, TimeSpan maxAge)
        {
            if (_cache.TryGetValue($"{cacheKey}_timestamp", out var timestamp) && timestamp is DateTime lastRefresh)
            {
                return DateTime.UtcNow - lastRefresh > maxAge;
            }
            return true; // No timestamp found, should refresh
        }

        // Track when cache was last refreshed
        public void UpdateCacheTimestamp(string cacheKey)
        {
            _cache.Set($"{cacheKey}_timestamp", DateTime.UtcNow, TimeSpan.FromDays(1));
        }

        public void InvalidateJobPostCaches()
        {
            var keysToRemove = _cacheKeys.Where(key =>
                key.StartsWith("filter_options:") ||
                key.StartsWith("total_count_") ||
                key.StartsWith("job_posts_"))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
                _cacheKeys.TryRemove(key);
            }

            // Also remove timestamp entries
            var timestampKeys = _cacheKeys.Where(key => key.EndsWith("_timestamp")).ToList();
            foreach (var key in timestampKeys)
            {
                _cache.Remove(key);
                _cacheKeys.TryRemove(key);
            }

            _logger.LogInformation("\n\t\t-> Invalidated [{Count}] cache entries", keysToRemove.Count + timestampKeys.Count);
        }
    }


    public class ConcurrentHashSet<T> : IDisposable
    {
        private readonly HashSet<T> _hashSet = new HashSet<T>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public bool Add(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                return _hashSet.Add(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryRemove(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                return _hashSet.Remove(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public IEnumerable<T> Where(Func<T, bool> predicate)
        {
            _lock.EnterReadLock();
            try
            {
                return _hashSet.Where(predicate).ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
}
