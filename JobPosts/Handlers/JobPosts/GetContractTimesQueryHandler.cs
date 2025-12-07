using JobPosts.Data;
using JobPosts.Queries.JobPosts;
using JobPosts.Services;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.JobPosts
{
    public class GetContractTimesQueryHandler : BaseFilterQueryHandler<GetContractTimesQuery, List<string>>, IRequestHandler<GetContractTimesQuery, List<string>>
    {
        public GetContractTimesQueryHandler(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            CacheInvalidationService cacheInvalidationService,
            ILogger<GetContractTimesQueryHandler> logger)
            : base(serviceProvider, cache, cacheInvalidationService, logger)
        {
        }

        public async Task<List<string>> Handle(GetContractTimesQuery request, CancellationToken cancellationToken)
        {
            var (fromDate, toDate) = CalculateDateRange(request.TimeframeInWeeks);
            var cacheKey = GenerateCacheKey("contract_times", request.CountryCode, request.TimeframeInWeeks, fromDate, toDate);

            if (_cache.TryGetValue(cacheKey, out List<string> cachedResult))
            {
                _logger.LogDebug("Returning cached contract times");
                return cachedResult;
            }

            _logger.LogDebug("Cache miss for contract times, querying DB");

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
                    .Where(j => j.Created >= fromDate && j.Created < toDate)
                    .Where(j => j.ContractTimeId != null);

                if (countryId.HasValue)
                {
                    query = query.Where(j => j.CountryId == countryId.Value);
                }

                // Get contract times using joins
                var result = await query
                    .Select(j => j.ContractTimeId)
                    .Distinct()
                    .Join(context.ContractTimes,
                        ctId => ctId,
                        ct => ct.Id,
                        (ctId, ct) => ct.Time)
                    .Where(time => !string.IsNullOrWhiteSpace(time))
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
