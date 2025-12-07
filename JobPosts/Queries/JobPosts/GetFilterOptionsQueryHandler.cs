using JobPosts.Data;
using JobPosts.DTOs.JobPosts;
using JobPosts.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace JobPosts.Queries.JobPosts
{
    public class GetFilterOptionsQueryHandler : IRequestHandler<GetFilterOptionsQuery, FilterOptionsDTO>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMemoryCache _cache;
        private readonly CacheInvalidationService _cacheInvalidationService;
        private readonly ILogger<GetFilterOptionsQueryHandler> _logger;

        public GetFilterOptionsQueryHandler(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            CacheInvalidationService cacheInvalidationService,
            ILogger<GetFilterOptionsQueryHandler> logger)
        {
            _serviceProvider = serviceProvider;
            _cache = cache;
            _cacheInvalidationService = cacheInvalidationService;
            _logger = logger;
        }

        public async Task<FilterOptionsDTO> Handle(GetFilterOptionsQuery request, CancellationToken cancellationToken)
        {
            DateTime fromDate, toDate;
            if (request.TimeframeInWeeks == 1)
            {
                fromDate = DateTime.UtcNow.AddDays(-7);
                toDate = DateTime.UtcNow;
            }
            else
            {
                var weeksAgo = request.TimeframeInWeeks - 1;
                toDate = DateTime.UtcNow.AddDays(-7 * weeksAgo);
                fromDate = toDate.AddDays(-7);
            }

            var normalizedCountryCode = string.IsNullOrWhiteSpace(request.CountryCode)
                ? null
                : request.CountryCode.Trim();
            var countryKey = normalizedCountryCode ?? "ALL";
            var cacheKey = $"filter_options:{countryKey}:{request.TimeframeInWeeks}:{fromDate:yyyyMMddHH}:{toDate:yyyyMMddHH}";

            if (_cache.TryGetValue(cacheKey, out FilterOptionsDTO cachedResult))
            {
                _logger.LogDebug("Returning cached filter options for {CountryKey}", countryKey);
                return cachedResult;
            }

            _logger.LogDebug("Cache miss for filter options {CountryKey}, querying DB", countryKey);

            var tasks = new Task<List<string>>[]
            {
                GetContractTypesAsync(normalizedCountryCode, fromDate, toDate, cancellationToken),
                GetContractTimesAsync(normalizedCountryCode, fromDate, toDate, cancellationToken),
                GetWorkLocationsAsync(normalizedCountryCode, fromDate, toDate, cancellationToken),
                GetCompaniesAsync(normalizedCountryCode, fromDate, toDate, cancellationToken),
                GetLocationsAsync(normalizedCountryCode, fromDate, toDate, cancellationToken),
                GetSkillsAsync(normalizedCountryCode, fromDate, toDate, cancellationToken),
                GetLanguagesAsync(normalizedCountryCode, fromDate, toDate, cancellationToken),
                GetCountriesAsync(cancellationToken)
            };

            var results = await Task.WhenAll(tasks);

            var dto = new FilterOptionsDTO
            {
                ContractTypes = results[0],
                ContractTimes = results[1],
                WorkLocations = results[2],
                Companies = results[3],
                Locations = results[4],
                Skills = results[5],
                Languages = results[6],
                Countries = results[7]
            };

            // Add message for no data scenario
            if (!dto.HasData)
            {
                var countryDisplayName = string.IsNullOrEmpty(normalizedCountryCode) ? "all countries" : normalizedCountryCode;
                var timeframeText = request.TimeframeInWeeks == 1 ? "the past week" : $"week {request.TimeframeInWeeks}";
                dto.Message = $"No filter options available for {countryDisplayName} in {timeframeText}.";
            }

            var cacheEntryOptions = new MemoryCacheEntryOptions
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

            _cache.Set(cacheKey, dto, cacheEntryOptions);
            _cacheInvalidationService.TrackCacheKey(cacheKey);

            return dto;
        }

        private async Task<List<string>> GetContractTypesAsync(string? countryCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();

            const int timeoutSeconds = 30;
            int? previousTimeout = context.Database.GetCommandTimeout();

            try
            {
                context.Database.SetCommandTimeout(timeoutSeconds);

                var query = context.JobPosts
                    .AsNoTracking()
                    .Where(j => j.Created >= fromDate && j.Created < toDate);

                if (!string.IsNullOrEmpty(countryCode))
                {
                    query = query.Where(j => j.Country.CountryCode == countryCode);
                }

                return await query
                    .Where(j => j.ContractType != null && !string.IsNullOrWhiteSpace(j.ContractType.Type))
                    .Select(j => j.ContractType.Type)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(cancellationToken);
            }
            finally
            {
                context.Database.SetCommandTimeout(previousTimeout);
            }
        }

        private async Task<List<string>> GetContractTimesAsync(string? countryCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();

            const int timeoutSeconds = 30;
            int? previousTimeout = context.Database.GetCommandTimeout();

            try
            {
                context.Database.SetCommandTimeout(timeoutSeconds);

                var query = context.JobPosts
                    .AsNoTracking()
                    .Where(j => j.Created >= fromDate && j.Created < toDate);

                if (!string.IsNullOrEmpty(countryCode))
                {
                    query = query.Where(j => j.Country.CountryCode == countryCode);
                }

                return await query
                    .Where(j => j.ContractTime != null && !string.IsNullOrWhiteSpace(j.ContractTime.Time))
                    .Select(j => j.ContractTime.Time)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(cancellationToken);
            }
            finally
            {
                context.Database.SetCommandTimeout(previousTimeout);
            }
        }

        private async Task<List<string>> GetWorkLocationsAsync(string? countryCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();

            const int timeoutSeconds = 30;
            int? previousTimeout = context.Database.GetCommandTimeout();

            try
            {
                context.Database.SetCommandTimeout(timeoutSeconds);

                var query = context.JobPosts
                    .AsNoTracking()
                    .Where(j => j.Created >= fromDate && j.Created < toDate);

                if (!string.IsNullOrEmpty(countryCode))
                {
                    query = query.Where(j => j.Country.CountryCode == countryCode);
                }

                return await query
                    .Where(j => j.WorkplaceModel != null && !string.IsNullOrWhiteSpace(j.WorkplaceModel.Workplace))
                    .Select(j => j.WorkplaceModel.Workplace)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(cancellationToken);
            }
            finally
            {
                context.Database.SetCommandTimeout(previousTimeout);
            }
        }

        private async Task<List<string>> GetCompaniesAsync(string? countryCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();

            const int timeoutSeconds = 30;
            int? previousTimeout = context.Database.GetCommandTimeout();
            try
            {
                context.Database.SetCommandTimeout(timeoutSeconds);

                var query = context.JobPosts
                    .AsNoTracking()
                    .Where(j => j.Created >= fromDate && j.Created < toDate);

                if (!string.IsNullOrEmpty(countryCode))
                {
                    query = query.Where(j => j.Country.CountryCode == countryCode);
                }

                return await query
                    .Where(j => j.Company != null && !string.IsNullOrWhiteSpace(j.Company.CompanyName))
                    .GroupBy(j => j.Company.CompanyName)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .ToListAsync(cancellationToken);
            }
            finally
            {
                context.Database.SetCommandTimeout(previousTimeout);
            }
        }

        private async Task<List<string>> GetLocationsAsync(string? countryCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();
            const int timeoutSeconds = 30;
            int? previousTimeout = context.Database.GetCommandTimeout();
            try
            {
                context.Database.SetCommandTimeout(timeoutSeconds);

                var query = context.JobPosts
                    .AsNoTracking()
                    .Where(j => j.Created >= fromDate && j.Created < toDate);

                if (!string.IsNullOrEmpty(countryCode))
                {
                    query = query.Where(j => j.Country.CountryCode == countryCode);
                }

                return await query
                    .Where(j => j.Location != null && !string.IsNullOrWhiteSpace(j.Location.LocationName))
                    .GroupBy(j => j.Location.LocationName)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .ToListAsync(cancellationToken);
            }
            finally
            {
                context.Database.SetCommandTimeout(previousTimeout);
            }
        }

        private async Task<List<string>> GetSkillsAsync(string? countryCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();
            const int timeoutSeconds = 30;
            int? previousTimeout = context.Database.GetCommandTimeout();
            try
            {
                context.Database.SetCommandTimeout(timeoutSeconds);

                var query = context.JobPosts
                    .AsNoTracking()
                    .Where(j => j.Created >= fromDate && j.Created < toDate);

                if (!string.IsNullOrEmpty(countryCode))
                {
                    query = query.Where(j => j.Country.CountryCode == countryCode);
                }

                return await query
                    .SelectMany(j => j.JobPostSkills)
                    .Where(js => !string.IsNullOrWhiteSpace(js.Skill.SkillName))
                    .GroupBy(js => js.Skill.SkillName)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .ToListAsync(cancellationToken);
            }
            finally
            {
                context.Database.SetCommandTimeout(previousTimeout);
            }
        }

        private async Task<List<string>> GetLanguagesAsync(string? countryCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();
            const int timeoutSeconds = 30;
            int? previousTimeout = context.Database.GetCommandTimeout();

            try
            {
                context.Database.SetCommandTimeout(timeoutSeconds);

                var query = context.JobPosts
                    .AsNoTracking()
                    .Where(j => j.Created >= fromDate && j.Created < toDate);

                if (!string.IsNullOrEmpty(countryCode))
                {
                    query = query.Where(j => j.Country.CountryCode == countryCode);
                }

                return await query
                    .SelectMany(j => j.JobPostLanguages)
                    .Where(jl => !string.IsNullOrWhiteSpace(jl.Language.Name))
                    .Select(jl => jl.Language.Name)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(cancellationToken);
            }
            finally
            {
                context.Database.SetCommandTimeout(previousTimeout);
            }
        }

        private async Task<List<string>> GetCountriesAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();
            const int timeoutSeconds = 30;
            int? previousTimeout = context.Database.GetCommandTimeout();

            try
            {
                context.Database.SetCommandTimeout(timeoutSeconds);

                return await context.Countries
                    .AsNoTracking()
                    .Where(c => !string.IsNullOrWhiteSpace(c.CountryName))
                    .Select(c => c.CountryName)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(cancellationToken);
            }
            finally
            {
                context.Database.SetCommandTimeout(previousTimeout);
            }
        }
    }
}
