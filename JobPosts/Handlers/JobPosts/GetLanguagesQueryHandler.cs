using JobPosts.Data;
using JobPosts.Queries.JobPosts;
using JobPosts.Services;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.JobPosts
{
    public class GetLanguagesQueryHandler : BaseFilterQueryHandler<GetLanguagesQuery, List<string>>, IRequestHandler<GetLanguagesQuery, List<string>>
    {
        public GetLanguagesQueryHandler(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            CacheInvalidationService cacheInvalidationService,
            ILogger<GetLanguagesQueryHandler> logger)
            : base(serviceProvider, cache, cacheInvalidationService, logger)
        {
        }

        public async Task<List<string>> Handle(GetLanguagesQuery request, CancellationToken cancellationToken)
        {
            var (fromDate, toDate) = CalculateDateRange(request.TimeframeInWeeks);
            var cacheKey = GenerateCacheKey("languages", request.CountryCode, request.TimeframeInWeeks, fromDate, toDate);

            if (_cache.TryGetValue(cacheKey, out List<string> cachedResult))
            {
                _logger.LogDebug("Returning cached languages");
                return cachedResult;
            }

            _logger.LogDebug("Cache miss for languages, querying DB");

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();

            SetupDatabaseTimeout(context, out int? previousTimeout);
            try
            {
                // Resolve country code to ID if provided
                int? countryId = null;
                if (!string.IsNullOrWhiteSpace(request.CountryCode))
                {
                    countryId = await context.Countries
                        .AsNoTracking()
                        .Where(c => c.CountryCode == request.CountryCode.Trim())
                        .Select(c => c.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (countryId == null)
                        return new List<string>();
                }

                // Build query using integer IDs
                var query = context.JobPosts
                    .AsNoTracking()
                    .Where(j => j.Created >= fromDate && j.Created < toDate);

                if (countryId.HasValue)
                {
                    query = query.Where(j => j.CountryId == countryId.Value);
                }

                // Get distinct languages using joins
                var result = await query
                    .SelectMany(j => j.JobPostLanguages.Select(jl => jl.LanguageId))
                    .Distinct()
                    .Join(context.Languages,
                        langId => langId,
                        l => l.Id,
                        (langId, l) => l.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
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
