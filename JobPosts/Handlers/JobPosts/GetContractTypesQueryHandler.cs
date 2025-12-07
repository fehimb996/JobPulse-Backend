using JobPosts.Data;
using JobPosts.Queries.JobPosts;
using JobPosts.Services;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.JobPosts
{
    public class GetContractTypesQueryHandler : BaseFilterQueryHandler<GetContractTypesQuery, List<string>>, IRequestHandler<GetContractTypesQuery, List<string>>
    {
        public GetContractTypesQueryHandler(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            CacheInvalidationService cacheInvalidationService,
            ILogger<GetContractTypesQueryHandler> logger)
            : base(serviceProvider, cache, cacheInvalidationService, logger)
        {
        }

        public async Task<List<string>> Handle(GetContractTypesQuery request, CancellationToken cancellationToken)
        {
            var (fromDate, toDate) = CalculateDateRange(request.TimeframeInWeeks);
            var cacheKey = GenerateCacheKey("contract_types", request.CountryCode, request.TimeframeInWeeks, fromDate, toDate);

            if (_cache.TryGetValue(cacheKey, out List<string> cachedResult))
            {
                _logger.LogDebug("Returning cached contract types");
                return cachedResult;
            }

            _logger.LogDebug("Cache miss for contract types, querying DB");

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
                    .Where(j => j.ContractTypeId != null);

                if (countryId.HasValue)
                {
                    query = query.Where(j => j.CountryId == countryId.Value);
                }

                // Get contract types using joins
                var result = await query
                    .Select(j => j.ContractTypeId)
                    .Distinct()
                    .Join(context.ContractTypes,
                        ctId => ctId,
                        ct => ct.Id,
                        (ctId, ct) => ct.Type)
                    .Where(type => !string.IsNullOrWhiteSpace(type))
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
