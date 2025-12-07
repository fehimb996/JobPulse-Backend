using JobPosts.Data;
using JobPosts.Queries.JobPosts;
using JobPosts.Services;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.JobPosts
{
    public class GetCompaniesQueryHandler : BaseFilterQueryHandler<GetCompaniesQuery, List<string>>, IRequestHandler<GetCompaniesQuery, List<string>>
    {
        public GetCompaniesQueryHandler(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            CacheInvalidationService cacheInvalidationService,
            ILogger<GetCompaniesQueryHandler> logger)
            : base(serviceProvider, cache, cacheInvalidationService, logger)
        {
        }

        public async Task<List<string>> Handle(GetCompaniesQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.CountryCode))
            {
                _logger.LogDebug("No country code provided, returning empty companies list");
                return new List<string>();
            }

            var (fromDate, toDate) = CalculateDateRange(request.TimeframeInWeeks);
            var cacheKey = GenerateCacheKey("companies", request.CountryCode, request.TimeframeInWeeks, fromDate, toDate);

            if (_cache.TryGetValue(cacheKey, out List<string> cachedResult))
            {
                _logger.LogDebug("Returning cached companies");
                return cachedResult;
            }

            _logger.LogDebug("Cache miss for companies, querying DB");

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

                // Get companies with counts using CompanyId
                var result = await context.JobPosts
                    .AsNoTracking()
                    .Where(j => j.Created >= fromDate && j.Created < toDate)
                    .Where(j => j.CountryId == countryId && j.CompanyId != null)
                    .GroupBy(j => j.CompanyId)
                    .Select(g => new { CompanyId = g.Key, Count = g.Count() })
                    .Join(context.Companies,
                        temp => temp.CompanyId,
                        c => c.Id,
                        (temp, c) => new { CompanyName = c.CompanyName, temp.Count })
                    .Where(x => !string.IsNullOrWhiteSpace(x.CompanyName))
                    .OrderByDescending(x => x.Count)
                    .Select(x => x.CompanyName)
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
