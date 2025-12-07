using JobPosts.Data;
using JobPosts.Queries.JobPosts;
using JobPosts.Services;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.JobPosts
{
    public class GetCountriesQueryHandler : BaseFilterQueryHandler<GetCountriesQuery, List<string>>, IRequestHandler<GetCountriesQuery, List<string>>
    {
        public GetCountriesQueryHandler(
        IServiceProvider serviceProvider,
        IMemoryCache cache,
        CacheInvalidationService cacheInvalidationService,
        ILogger<GetCountriesQueryHandler> logger)
        : base(serviceProvider, cache, cacheInvalidationService, logger)
        {
        }

        public async Task<List<string>> Handle(GetCountriesQuery request, CancellationToken cancellationToken)
        {
            const string cacheKey = "countries:all";

            if (_cache.TryGetValue(cacheKey, out List<string> cachedResult))
            {
                _logger.LogDebug("Returning cached countries");
                return cachedResult;
            }

            _logger.LogDebug("Cache miss for countries, querying DB");

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();

            SetupDatabaseTimeout(context, out int? previousTimeout);
            try
            {
                var result = await context.Countries
                    .AsNoTracking()
                    .Where(c => !string.IsNullOrWhiteSpace(c.CountryName))
                    .Select(c => c.CountryName)
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
