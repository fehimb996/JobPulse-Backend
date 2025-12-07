using JobPosts.Data;
using JobPosts.Queries.JobPosts;
using JobPosts.Services;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.JobPosts
{
    public class GetSkillsQueryHandler : BaseFilterQueryHandler<GetSkillsQuery, List<string>>, IRequestHandler<GetSkillsQuery, List<string>>
    {
        public GetSkillsQueryHandler(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            CacheInvalidationService cacheInvalidationService,
            ILogger<GetSkillsQueryHandler> logger)
            : base(serviceProvider, cache, cacheInvalidationService, logger)
        {
        }

        public async Task<List<string>> Handle(GetSkillsQuery request, CancellationToken cancellationToken)
        {
            var (fromDate, toDate) = CalculateDateRange(request.TimeframeInWeeks);
            var cacheKey = GenerateCacheKey("skills", request.CountryCode, request.TimeframeInWeeks, fromDate, toDate);

            if (_cache.TryGetValue(cacheKey, out List<string> cachedResult))
            {
                _logger.LogDebug("Returning cached skills");
                return cachedResult;
            }

            _logger.LogDebug("Cache miss for skills, querying DB");

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

                // Get skills with counts using joins for better performance
                var result = await query
                    .SelectMany(j => j.JobPostSkills.Select(js => js.SkillId))
                    .GroupBy(skillId => skillId)
                    .Select(g => new { SkillId = g.Key, Count = g.Count() })
                    .Join(context.Skills,
                        temp => temp.SkillId,
                        s => s.Id,
                        (temp, s) => new { SkillName = s.SkillName, temp.Count })
                    .Where(x => !string.IsNullOrWhiteSpace(x.SkillName))
                    .OrderByDescending(x => x.Count)
                    .Select(x => x.SkillName)
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
