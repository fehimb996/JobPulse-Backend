using JobPosts.Data;
using JobPosts.Queries.JobPosts;
using JobPosts.Services;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.JobPosts
{
    public class GetWorkLocationsQueryHandler : BaseFilterQueryHandler<GetWorkLocationsQuery, List<string>>, IRequestHandler<GetWorkLocationsQuery, List<string>>
    {
        public GetWorkLocationsQueryHandler(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            CacheInvalidationService cacheInvalidationService,
            ILogger<GetWorkLocationsQueryHandler> logger)
            : base(serviceProvider, cache, cacheInvalidationService, logger)
        {
        }

        public async Task<List<string>> Handle(GetWorkLocationsQuery request, CancellationToken cancellationToken)
        {
            var (fromDate, toDate) = CalculateDateRange(request.TimeframeInWeeks);
            var cacheKey = GenerateCacheKey("work_locations", request.CountryCode, request.TimeframeInWeeks, fromDate, toDate);

            if (_cache.TryGetValue(cacheKey, out List<string> cachedResult))
            {
                _logger.LogDebug("Returning cached work locations");
                return cachedResult;
            }

            _logger.LogDebug("Cache miss for work locations, querying DB");

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();

            SetupDatabaseTimeout(context, out int? previousTimeout);
            try
            {
                // First resolve country code to ID if provided
                int? countryId = null;
                if (!string.IsNullOrWhiteSpace(request.CountryCode))
                {
                    countryId = await context.Countries
                        .AsNoTracking()
                        .Where(c => c.CountryCode == request.CountryCode.Trim())
                        .Select(c => c.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (countryId == null)
                        return new List<string>(); // Country not found
                }

                // Build query using integer IDs
                var query = context.JobPosts
                    .AsNoTracking()
                    .Where(j => j.Created >= fromDate && j.Created < toDate)
                    .Where(j => j.WorkplaceModelId != null);

                if (countryId.HasValue)
                {
                    query = query.Where(j => j.CountryId == countryId.Value);
                }

                // Join with WorkplaceModels to get the actual workplace names
                var result = await query
                    .Join(context.WorkplaceModels,
                        jp => jp.WorkplaceModelId,
                        wm => wm.Id,
                        (jp, wm) => wm.Workplace)
                    .Where(workplace => !string.IsNullOrWhiteSpace(workplace))
                    .Distinct()
                    .OrderBy(x => x)
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
