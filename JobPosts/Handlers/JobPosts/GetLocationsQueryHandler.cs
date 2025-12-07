using JobPosts.Data;
using JobPosts.Queries.JobPosts;
using JobPosts.Services;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.JobPosts
{
    public class GetLocationsQueryHandler : BaseFilterQueryHandler<GetLocationsQuery, List<string>>, IRequestHandler<GetLocationsQuery, List<string>>
    {
        public GetLocationsQueryHandler(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            CacheInvalidationService cacheInvalidationService,
            ILogger<GetLocationsQueryHandler> logger)
            : base(serviceProvider, cache, cacheInvalidationService, logger)
        {
        }

        public async Task<List<string>> Handle(GetLocationsQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.CountryCode))
            {
                _logger.LogDebug("No country code provided, returning empty locations list");
                return new List<string>();
            }

            var (fromDate, toDate) = CalculateDateRange(request.TimeframeInWeeks);
            var cacheKey = GenerateCacheKey("locations", request.CountryCode, request.TimeframeInWeeks, fromDate, toDate);

            if (_cache.TryGetValue(cacheKey, out List<string> cachedResult))
            {
                _logger.LogDebug("Returning cached locations");
                return cachedResult;
            }

            _logger.LogDebug("Cache miss for locations, querying DB");

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();

            SetupDatabaseTimeout(context, out int? previousTimeout);
            try
            {
                // Resolve country code to ID
                var countryId = await context.Countries
                    .AsNoTracking()
                    .Where(c => c.CountryCode == request.CountryCode.Trim())
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (countryId == 0)
                    return new List<string>();

                // Get locations with counts using LocationId
                var result = await context.JobPosts
                    .AsNoTracking()
                    .Where(j => j.Created >= fromDate && j.Created < toDate)
                    .Where(j => j.CountryId == countryId && j.LocationId != null)
                    .GroupBy(j => j.LocationId)
                    .Select(g => new { LocationId = g.Key, Count = g.Count() })
                    .Join(context.Locations,
                        temp => temp.LocationId,
                        l => l.Id,
                        (temp, l) => new { LocationName = l.LocationName, temp.Count })
                    .Where(x => !string.IsNullOrWhiteSpace(x.LocationName))
                    .OrderByDescending(x => x.Count)
                    .Select(x => x.LocationName)
                    .ToListAsync(cancellationToken);

                _cache.Set(cacheKey, result, CreateCacheOptions());
                _cacheInvalidationService.TrackCacheKey(cacheKey);

                return result;
            }
            finally
            {
                RestoreDatabaseTimeout(context, previousTimeout);
            }
        }
    }
}
