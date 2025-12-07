using JobPosts.Data;
using JobPosts.DTOs.JobPosts;
using JobPosts.Models;
using JobPosts.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace JobPosts.Queries.JobPosts
{
    public class GetJobPostsQueryHandler : IRequestHandler<GetJobPostsQuery, JobPostPagedResultDTO>
    {
        private readonly JobPostsDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly CacheInvalidationService _cacheInvalidationService;
        private readonly ILogger<GetJobPostsQueryHandler> _logger;

        public GetJobPostsQueryHandler(
            JobPostsDbContext context,
            IMemoryCache cache,
            CacheInvalidationService cacheInvalidationService,
            ILogger<GetJobPostsQueryHandler> logger)
        {
            _context = context;
            _cache = cache;
            _cacheInvalidationService = cacheInvalidationService;
            _logger = logger;
        }

        public async Task<JobPostPagedResultDTO> Handle(GetJobPostsQuery request, CancellationToken cancellationToken)
        {
            const int extendedTimeoutSeconds = 120;

            // Compute timeframe
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

            int? previousTimeout = _context.Database.GetCommandTimeout();
            try
            {
                _context.Database.SetCommandTimeout(extendedTimeoutSeconds);

                // Resolve string filters to IDs (country, company, location, contract types, skills, languages)
                int? countryId = null;
                if (!string.IsNullOrWhiteSpace(request.CountryCode))
                {
                    var country = await _context.Countries
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.CountryCode == request.CountryCode, cancellationToken);
                    countryId = country?.Id;
                    if (request.CountryCode != null && countryId == null)
                        return CreateEmptyResult(request, 0, 0, BuildNoResultsMessage(request));
                }

                List<int>? companyIds = null;
                if (!string.IsNullOrWhiteSpace(request.Company))
                {
                    var companySearch = request.Company.Trim().ToLowerInvariant();
                    companyIds = await _context.Companies
                        .AsNoTracking()
                        .Where(c => c.CompanyName != null && EF.Functions.Like(c.CompanyName.ToLower(), $"%{companySearch}%"))
                        .Select(c => c.Id)
                        .ToListAsync(cancellationToken);

                    if (companyIds.Count == 0)
                        return CreateEmptyResult(request, 0, 0, BuildNoResultsMessage(request));
                }

                List<int>? locationIds = null;
                if (!string.IsNullOrWhiteSpace(request.Location))
                {
                    var locationSearch = request.Location.Trim().ToLowerInvariant();
                    locationIds = await _context.Locations
                        .AsNoTracking()
                        .Where(l => l.LocationName != null && EF.Functions.Like(l.LocationName.ToLower(), $"%{locationSearch}%"))
                        .Select(l => l.Id)
                        .ToListAsync(cancellationToken);

                    if (locationIds.Count == 0)
                        return CreateEmptyResult(request, 0, 0, BuildNoResultsMessage(request));
                }

                int? contractTypeId = null;
                if (!string.IsNullOrWhiteSpace(request.ContractType))
                {
                    var ct = await _context.ContractTypes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Type == request.ContractType, cancellationToken);
                    if (ct == null)
                        return CreateEmptyResult(request, 0, 0, BuildNoResultsMessage(request));
                    contractTypeId = ct.Id;
                }

                int? contractTimeId = null;
                if (!string.IsNullOrWhiteSpace(request.ContractTime))
                {
                    var ct = await _context.ContractTimes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Time == request.ContractTime, cancellationToken);
                    if (ct == null)
                        return CreateEmptyResult(request, 0, 0, BuildNoResultsMessage(request));
                    contractTimeId = ct.Id;
                }

                int? workplaceModelId = null;
                if (!string.IsNullOrWhiteSpace(request.WorkLocation))
                {
                    var wm = await _context.WorkplaceModels
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Workplace == request.WorkLocation, cancellationToken);
                    if (wm == null)
                        return CreateEmptyResult(request, 0, 0, BuildNoResultsMessage(request));
                    workplaceModelId = wm.Id;
                }

                List<int>? skillIds = null;
                if (request.Skills?.Any() == true)
                {
                    var lowered = request.Skills.Select(s => s.Trim().ToLowerInvariant()).ToList();
                    skillIds = await _context.Skills
                        .AsNoTracking()
                        .Where(s => lowered.Contains(s.SkillName.ToLower()))
                        .Select(s => s.Id)
                        .ToListAsync(cancellationToken);

                    if (skillIds.Count == 0)
                        return CreateEmptyResult(request, 0, 0, BuildNoResultsMessage(request));
                }

                List<int>? languageIds = null;
                if (request.Languages?.Any() == true)
                {
                    var lowered = request.Languages.Select(l => l.Trim().ToLowerInvariant()).ToList();
                    languageIds = await _context.Languages
                        .AsNoTracking()
                        .Where(l => lowered.Contains(l.Name.ToLower()))
                        .Select(l => l.Id)
                        .ToListAsync(cancellationToken);

                    if (languageIds.Count == 0)
                        return CreateEmptyResult(request, 0, 0, BuildNoResultsMessage(request));
                }

                // Build main query (ID-based filters)
                var mainQuery = BuildMainQuery(request, fromDate, toDate, countryId, companyIds, locationIds, contractTypeId, contractTimeId, workplaceModelId, skillIds, languageIds);

                // Cached count
                var totalCount = await GetCachedTotalCount(mainQuery, request, fromDate, toDate, cancellationToken);
                if (totalCount == 0)
                    return CreateEmptyResult(request, 0, 0, BuildNoResultsMessage(request));

                var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

                // Page primary keys (int) — select PK Id for joins using regular offset pagination
                var pageIds = await GetPageIds(mainQuery, request, cancellationToken);

                if (pageIds.Count == 0)
                    return CreateEmptyResult(request, totalCount, totalPages, $"No results found on page {request.Page}. Try a different page or adjust your filters.");

                // Load page items (batch child collections)
                var posts = await GetPageItems(pageIds, cancellationToken);
                var orderedPosts = OrderPageItems(posts, pageIds);

                // Pagination metadata (regular pagination)
                bool hasNextPage = request.Page < totalPages;

                return new JobPostPagedResultDTO
                {
                    Posts = orderedPosts,
                    TotalCount = totalCount,
                    TotalPages = totalPages,
                    Message = null,
                    HasNextPage = hasNextPage
                };
            }
            finally
            {
                _context.Database.SetCommandTimeout(previousTimeout);
            }
        }

        // BuildMainQuery (ID-based filters)
        private IQueryable<JobPost> BuildMainQuery(
            GetJobPostsQuery request,
            DateTime fromDate,
            DateTime toDate,
            int? countryId,
            List<int>? companyIds,
            List<int>? locationIds,
            int? contractTypeId,
            int? contractTimeId,
            int? workplaceModelId,
            List<int>? skillIds,
            List<int>? languageIds)
        {
            var q = _context.JobPosts
                .AsNoTracking()
                .Where(j => j.Created >= fromDate && j.Created < toDate);

            if (countryId.HasValue)
                q = q.Where(j => j.CountryId == countryId.Value);

            if (request.OnlyFavorites && !string.IsNullOrEmpty(request.UserId))
            {
                var userId = request.UserId;
                q = q.Where(j => _context.UserFavoriteJobs
                               .Any(ufj => ufj.UserId == userId && ufj.JobPostId == j.Id));
            }

            if (companyIds != null)
                q = q.Where(j => j.CompanyId.HasValue && companyIds.Contains(j.CompanyId.Value));

            if (locationIds != null)
                q = q.Where(j => j.LocationId.HasValue && locationIds.Contains(j.LocationId.Value));

            if (contractTypeId.HasValue) q = q.Where(j => j.ContractTypeId == contractTypeId.Value);
            if (contractTimeId.HasValue) q = q.Where(j => j.ContractTimeId == contractTimeId.Value);
            if (workplaceModelId.HasValue) q = q.Where(j => j.WorkplaceModelId == workplaceModelId.Value);

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                var normalizedTitle = JobPost.NormalizeTitle(request.Title);
                q = q.Where(j => j.TitleNormalized != null && EF.Functions.Like(j.TitleNormalized, $"%{normalizedTitle}%"));
            }

            // skills: require job contains all skillIds (match by PK JobPost.Id)
            if (skillIds != null && skillIds.Count > 0)
            {
                q = q.Where(j =>
                    _context.JobPostSkills
                        .Where(js => js.JobPostId == j.Id && skillIds.Contains(js.SkillId))
                        .Select(js => js.SkillId)
                        .Distinct()
                        .Count() == skillIds.Count);
            }

            // languages: at least one match (by PK JobPost.Id)
            if (languageIds != null && languageIds.Count > 0)
            {
                q = q.Where(j =>
                    _context.JobPostLanguages
                        .Any(jl => jl.JobPostId == j.Id && languageIds.Contains(jl.LanguageId)));
            }

            return q;
        }

        // Regular offset pagination for page PK selection
        private async Task<List<int>> GetPageIds(IQueryable<JobPost> query, GetJobPostsQuery request, CancellationToken cancellationToken)
        {
            return await query
                .OrderByDescending(j => j.Created)
                .ThenBy(j => j.Id)
                .Select(j => j.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);
        }

        private async Task<int> GetCachedTotalCount(
    IQueryable<JobPost> query,
    GetJobPostsQuery request,
    DateTime fromDate,
    DateTime toDate,
    CancellationToken cancellationToken)
        {
            // Favorites count changes too often -> skip cache
            if (request.OnlyFavorites)
            {
                return await query.CountAsync(cancellationToken);
            }

            var cacheKey = $"total_count_{GetQueryHash(request, fromDate, toDate)}";
            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                var hasComplexFilters = !string.IsNullOrWhiteSpace(request.Title) ||
                                       (request.Skills != null && request.Skills.Any()) ||
                                       (request.Languages != null && request.Languages.Any());

                entry.AbsoluteExpirationRelativeToNow = hasComplexFilters
                    ? TimeSpan.FromMinutes(15)
                    : TimeSpan.FromHours(8);

                entry.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
                {
                    EvictionCallback = (key, value, reason, state) =>
                    {
                        if (state is CacheInvalidationService invalidationService && key != null)
                        {
                            invalidationService.RemoveCacheKey(key.ToString());
                        }
                    },
                    State = _cacheInvalidationService
                });

                return await query.CountAsync(cancellationToken);
            });

            _cacheInvalidationService.TrackCacheKey(cacheKey);
            return result;
        }

        // Load page items using PK list (int)
        private async Task<List<JobPostDTO>> GetPageItems(List<int> pageIds, CancellationToken cancellationToken)
        {
            // 1) Load base rows (small projection) including PK Id
            var baseRows = await _context.JobPosts
                .AsNoTracking()
                .Where(j => pageIds.Contains(j.Id))
                .Select(j => new
                {
                    j.Id,
                    j.Title,
                    j.Description,
                    j.Url,
                    j.SalaryMin,
                    j.SalaryMax,
                    j.Created,
                    CountryName = j.Country != null ? j.Country.CountryName : null,
                    CompanyName = j.Company != null ? j.Company.CompanyName : null,
                    CompanyUrl = j.Company != null ? j.Company.Url : null,
                    LocationName = j.Location != null ? j.Location.LocationName : null,
                    ContractTypeId = j.ContractTypeId,
                    ContractTimeId = j.ContractTimeId,
                    WorkLocation = j.WorkplaceModel != null ? j.WorkplaceModel.Workplace : null
                })
                .ToListAsync(cancellationToken);

            if (!baseRows.Any())
                return new List<JobPostDTO>();

            // 2) Preserve requested order by PK list
            var pos = pageIds.Select((id, idx) => new { id, idx }).ToDictionary(x => x.id, x => x.idx);
            var baseRowsOrdered = baseRows.OrderBy(r => pos.ContainsKey(r.Id) ? pos[r.Id] : int.MaxValue).ToList();

            var pkList = baseRowsOrdered.Select(r => r.Id).ToList();

            // 3) Batch fetch skills & languages by PK
            var skillsLookup = await _context.JobPostSkills
                .AsNoTracking()
                .Where(js => pkList.Contains(js.JobPostId))
                .Select(js => new { js.JobPostId, SkillName = js.Skill.SkillName })
                .ToListAsync(cancellationToken);

            var languagesLookup = await _context.JobPostLanguages
                .AsNoTracking()
                .Where(jl => pkList.Contains(jl.JobPostId))
                .Select(jl => new { jl.JobPostId, LanguageName = jl.Language.Name })
                .ToListAsync(cancellationToken);

            var skillsByPk = skillsLookup.GroupBy(x => x.JobPostId).ToDictionary(g => g.Key, g => g.Select(x => x.SkillName).ToList());
            var languagesByPk = languagesLookup.GroupBy(x => x.JobPostId).ToDictionary(g => g.Key, g => g.Select(x => x.LanguageName).ToList());

            // 4) Prefetch contract maps
            var contractTypeIds = baseRowsOrdered.Select(r => r.ContractTypeId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var contractTimeIds = baseRowsOrdered.Select(r => r.ContractTimeId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

            var contractTypeMap = contractTypeIds.Any()
                ? await _context.ContractTypes.AsNoTracking().Where(ct => contractTypeIds.Contains(ct.Id)).ToDictionaryAsync(ct => ct.Id, ct => ct.Type, cancellationToken)
                : new Dictionary<int, string>();

            var contractTimeMap = contractTimeIds.Any()
                ? await _context.ContractTimes.AsNoTracking().Where(ct => contractTimeIds.Contains(ct.Id)).ToDictionaryAsync(ct => ct.Id, ct => ct.Time, cancellationToken)
                : new Dictionary<int, string>();

            // 5) Map DTOs preserving order
            var dtos = baseRowsOrdered.Select(r => new JobPostDTO
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                Url = r.Url,
                SalaryMin = r.SalaryMin,
                SalaryMax = r.SalaryMax,
                Created = r.Created,
                CountryName = r.CountryName,
                CompanyName = r.CompanyName,
                CompanyUrl = r.CompanyUrl,
                LocationName = r.LocationName,
                ContractType = r.ContractTypeId.HasValue && contractTypeMap.TryGetValue(r.ContractTypeId.Value, out var ct) ? ct : null,
                ContractTime = r.ContractTimeId.HasValue && contractTimeMap.TryGetValue(r.ContractTimeId.Value, out var ctime) ? ctime : null,
                WorkLocation = r.WorkLocation,
                Skills = skillsByPk.TryGetValue(r.Id, out var sk) ? sk : new List<string>(),
                Languages = languagesByPk.TryGetValue(r.Id, out var ln) ? ln : new List<string>()
            }).ToList();

            return dtos;
        }

        // Ordering already applied; return as-is (defensive ordering can be added)
        private static List<JobPostDTO> OrderPageItems(List<JobPostDTO> pageItems, List<int> pageIds)
        {
            return pageItems;
        }

        // Create empty result helper
        private static JobPostPagedResultDTO CreateEmptyResult(GetJobPostsQuery request, int totalCount, int totalPages, string? message = null)
        {
            return new JobPostPagedResultDTO
            {
                Posts = new List<JobPostDTO>(),
                TotalCount = totalCount,
                TotalPages = totalPages,
                Message = message ?? BuildNoResultsMessage(request),
                HasNextPage = request.Page < (totalPages == 0 ? 1 : totalPages)
            };
        }

        // Query hashing unchanged
        private static string GetQueryHash(GetJobPostsQuery request, DateTime fromDate, DateTime toDate)
        {
            var keyParts = new List<string>
        {
            $"country:{request.CountryCode ?? "ALL"}",
            $"weeks:{request.TimeframeInWeeks}",
            $"from:{fromDate:yyyyMMddHH}",
            $"to:{toDate:yyyyMMddHH}",
            $"contract_type:{request.ContractType ?? "ANY"}",
            $"contract_time:{request.ContractTime ?? "ANY"}",
            $"work_location:{request.WorkLocation ?? "ANY"}",
            $"title:{request.Title ?? "ANY"}",
            $"company:{request.Company ?? "ANY"}",
            $"location:{request.Location ?? "ANY"}",
            $"skills:{(request.Skills?.Any() == true ? string.Join(",", request.Skills.OrderBy(s => s)) : "NONE")}",
            $"languages:{(request.Languages?.Any() == true ? string.Join(",", request.Languages.OrderBy(l => l)) : "NONE")}",
            $"favorites:{request.OnlyFavorites}",
            $"user:{request.UserId ?? "NONE"}"
        };

            var combinedKey = string.Join("|", keyParts);
            if (combinedKey.Length > 200)
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combinedKey));
                return Convert.ToBase64String(hashBytes)[..16];
            }

            return combinedKey.GetHashCode().ToString();
        }

        // No-results message unchanged
        private static string BuildNoResultsMessage(GetJobPostsQuery request)
        {
            var filters = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.CountryCode))
                filters.Add($"country: {request.CountryCode}");
            if (!string.IsNullOrWhiteSpace(request.Title))
                filters.Add($"title: \"{request.Title}\"");
            if (!string.IsNullOrWhiteSpace(request.Company))
                filters.Add($"company: \"{request.Company}\"");
            if (!string.IsNullOrWhiteSpace(request.Location))
                filters.Add($"location: \"{request.Location}\"");
            if (!string.IsNullOrWhiteSpace(request.ContractType))
                filters.Add($"contract type: {request.ContractType}");
            if (!string.IsNullOrWhiteSpace(request.ContractTime))
                filters.Add($"contract time: {request.ContractTime}");
            if (!string.IsNullOrWhiteSpace(request.WorkLocation))
                filters.Add($"work location: {request.WorkLocation}");
            if (request.Skills?.Any() == true)
                filters.Add($"skills: {string.Join(", ", request.Skills)}");
            if (request.Languages?.Any() == true)
                filters.Add($"languages: {string.Join(", ", request.Languages)}");
            if (request.OnlyFavorites)
                filters.Add("favorites only");

            var timeframeText = request.TimeframeInWeeks == 1 ? "the past week" : $"week {request.TimeframeInWeeks}";

            if (filters.Any())
            {
                return $"No job posts found for {timeframeText} with the selected filters: {string.Join(", ", filters)}. Try adjusting your search criteria.";
            }

            return $"No job posts found for {timeframeText}. Please try a different time period.";
        }
    }
}










// Backup 1
//public class GetJobPostsQueryHandler : IRequestHandler<GetJobPostsQuery, JobPostPagedResultDTO>
//{
//    private readonly JobPostsDbContext _context;
//    private readonly IMemoryCache _cache;
//    private readonly ILogger<GetJobPostsQueryHandler> _logger;

//    public GetJobPostsQueryHandler(JobPostsDbContext context, IMemoryCache cache, ILogger<GetJobPostsQueryHandler> logger)
//    {
//        _context = context;
//        _cache = cache;
//        _logger = logger;
//    }

//    public async Task<JobPostPagedResultDTO> Handle(GetJobPostsQuery request, CancellationToken cancellationToken)
//    {
//        const int extendedTimeoutSeconds = 600;
//        var fromDate = DateTime.UtcNow.AddDays(-7 * request.TimeframeInWeeks);

//        // Generate cache key for filter counts (excluding pagination)
//        var filterCacheKey = GenerateFilterCacheKey(request, fromDate);

//        int? previousTimeout = _context.Database.GetCommandTimeout();
//        try
//        {
//            _context.Database.SetCommandTimeout(extendedTimeoutSeconds);

//            // Build the main query
//            var query = BuildMainQuery(request, fromDate);

//            // Get paginated results first (fastest operation)
//            var pageIds = await query
//                .OrderByDescending(j => j.Created)
//                .ThenBy(j => j.JobId)
//                .Select(j => j.JobId)
//                .Skip((request.Page - 1) * request.PageSize)
//                .Take(request.PageSize)
//                .ToListAsync(cancellationToken);

//            if (pageIds.Count == 0)
//                return new JobPostPagedResultDTO { Posts = new List<JobPostDTO>(), TotalCount = 0, TotalPages = 0 };

//            // Get total count and page items sequentially (DbContext is not thread-safe)
//            var totalCount = await query.CountAsync(cancellationToken);
//            var pageItems = await GetPageItems(pageIds, cancellationToken);

//            var posts = OrderPageItems(pageItems, pageIds);

//            // Try to get filter counts from cache
//            var filterCounts = await _cache.GetOrCreateAsync(filterCacheKey, async entry =>
//            {
//                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5); // Cache for 5 minutes
//                _logger.LogInformation("Computing filter counts for cache key: {CacheKey}", filterCacheKey);

//                return await ComputeFilterCounts(request, fromDate, cancellationToken);
//            });

//            // Compute favorite count separately (user-specific, can't cache)
//            var favoriteCount = await ComputeFavoriteCount(request, fromDate, cancellationToken);

//            return new JobPostPagedResultDTO
//            {
//                Posts = posts,
//                TotalCount = totalCount,
//                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
//                ContractTypeCounts = filterCounts.ContractTypeCounts,
//                ContractTimeCounts = filterCounts.ContractTimeCounts,
//                WorkLocationCounts = filterCounts.WorkLocationCounts,
//                CompanyCounts = filterCounts.CompanyCounts,
//                LocationCounts = filterCounts.LocationCounts,
//                SkillCounts = filterCounts.SkillCounts,
//                LanguageCounts = filterCounts.LanguageCounts,
//                CountryCounts = filterCounts.CountryCounts,
//                FavoriteCount = favoriteCount
//            };
//        }
//        finally
//        {
//            _context.Database.SetCommandTimeout(previousTimeout);
//        }
//    }

//    private IQueryable<JobPost> BuildMainQuery(GetJobPostsQuery request, DateTime fromDate)
//    {
//        var query = _context.JobPosts
//            .AsNoTracking()
//            .Where(j => j.Created >= fromDate);

//        if (!string.IsNullOrWhiteSpace(request.CountryCode))
//            query = query.Where(j => j.Country.CountryCode == request.CountryCode);

//        if (request.OnlyFavorites && !string.IsNullOrEmpty(request.UserId))
//            query = query.Where(j => j.UsersWhoFavorited.Any(u => u.Id == request.UserId));

//        if (!string.IsNullOrWhiteSpace(request.ContractType))
//            query = query.Where(j => j.ContractType != null && j.ContractType.Type == request.ContractType);

//        if (!string.IsNullOrWhiteSpace(request.ContractTime))
//            query = query.Where(j => j.ContractTime != null && j.ContractTime.Time == request.ContractTime);

//        if (!string.IsNullOrWhiteSpace(request.WorkLocation))
//            query = query.Where(j => j.WorkplaceModel != null && j.WorkplaceModel.Workplace == request.WorkLocation);

//        if (!string.IsNullOrWhiteSpace(request.Title))
//        {
//            var normalizedInput = request.Title.ToLowerInvariant()
//                .Replace(" ", "").Replace("-", "").Replace("_", "");
//            var pattern = $"%{normalizedInput}%";
//            query = query.Where(j => EF.Functions.Like(j.TitleNormalized, pattern));
//        }

//        // Only apply company and location filters if a country is selected
//        if (!string.IsNullOrWhiteSpace(request.CountryCode))
//        {
//            if (!string.IsNullOrWhiteSpace(request.Company))
//            {
//                query = query.Where(j => j.Company != null &&
//                    EF.Functions.Collate(j.Company.CompanyName!, "Latin1_General_CI_AI").Contains(request.Company));
//            }

//            if (!string.IsNullOrWhiteSpace(request.Location))
//            {
//                query = query.Where(j => j.Location != null &&
//                    EF.Functions.Collate(j.Location.LocationName!, "Latin1_General_CI_AI").Contains(request.Location));
//            }
//        }

//        if (request.Skills != null && request.Skills.Any())
//        {
//            foreach (var skill in request.Skills)
//            {
//                var skillLower = skill.ToLower();
//                query = query.Where(j => j.JobPostSkills.Any(js => js.Skill.SkillName.ToLower() == skillLower));
//            }
//        }

//        if (request.Languages != null && request.Languages.Any())
//        {
//            var requestedLanguagesLower = request.Languages.Select(l => l.ToLower()).ToList();
//            query = query.Where(j => j.JobPostLanguages.Any(jl => requestedLanguagesLower.Contains(jl.Language.Name.ToLower())));
//        }

//        return query;
//    }

//    private async Task<List<JobPostDTO>> GetPageItems(List<long> pageIds, CancellationToken cancellationToken)
//    {
//        return await _context.JobPosts
//            .AsNoTracking()
//            .Where(j => pageIds.Contains(j.JobId))
//            .Select(j => new JobPostDTO
//            {
//                JobId = j.JobId,
//                Title = j.Title,
//                Description = j.Description,
//                Url = j.Url,
//                SalaryMin = j.SalaryMin,
//                SalaryMax = j.SalaryMax,
//                Created = j.Created,
//                CountryName = j.Country.CountryName,
//                CompanyName = j.Company != null ? j.Company.CompanyName : null,
//                LocationName = j.Location != null ? j.Location.LocationName : null,
//                ContractType = j.ContractType != null ? j.ContractType.Type : null,
//                ContractTime = j.ContractTime != null ? j.ContractTime.Time : null,
//                WorkLocation = j.WorkplaceModel != null ? j.WorkplaceModel.Workplace : null,
//                Skills = j.JobPostSkills.Select(js => js.Skill.SkillName).ToList(),
//                Languages = j.JobPostLanguages.Select(jl => jl.Language.Name).ToList()
//            })
//            .ToListAsync(cancellationToken);
//    }

//    private List<JobPostDTO> OrderPageItems(List<JobPostDTO> pageItems, List<long> pageIds)
//    {
//        var pos = pageIds.Select((id, i) => new { id, i }).ToDictionary(x => x.id, x => x.i);
//        return pageItems.OrderBy(p => pos[p.JobId]).ToList();
//    }

//    private async Task<FilterCountsResult> ComputeFilterCounts(GetJobPostsQuery request, DateTime fromDate, CancellationToken cancellationToken)
//    {
//        var query = BuildMainQuery(request, fromDate);

//        // Execute filter count queries sequentially to avoid DbContext concurrency issues
//        var contractTypeCounts = await query
//            .Where(j => j.ContractType != null)
//            .GroupBy(j => j.ContractType!.Type)
//            .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//            .OrderByDescending(c => c.Count)
//            .Take(50) // Limit to top 50 for performance
//            .ToListAsync(cancellationToken);

//        var contractTimeCounts = await query
//            .Where(j => j.ContractTime != null)
//            .GroupBy(j => j.ContractTime!.Time)
//            .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//            .OrderByDescending(c => c.Count)
//            .Take(50)
//            .ToListAsync(cancellationToken);

//        var workLocationCounts = await query
//            .Where(j => j.WorkplaceModel != null)
//            .GroupBy(j => j.WorkplaceModel!.Workplace)
//            .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//            .OrderByDescending(c => c.Count)
//            .Take(50)
//            .ToListAsync(cancellationToken);

//        var skillCounts = await query
//            .SelectMany(j => j.JobPostSkills)
//            .GroupBy(js => js.Skill.SkillName)
//            .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//            .OrderByDescending(c => c.Count)
//            .Take(50)
//            .ToListAsync(cancellationToken);

//        var languageCounts = await query
//            .SelectMany(j => j.JobPostLanguages)
//            .GroupBy(jl => jl.Language.Name)
//            .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//            .OrderByDescending(c => c.Count)
//            .Take(50)
//            .ToListAsync(cancellationToken);

//        // Country-specific counts
//        var companyCounts = new List<FilterCountDTO>();
//        var locationCounts = new List<FilterCountDTO>();
//        var countryCounts = new List<FilterCountDTO>();

//        if (!string.IsNullOrWhiteSpace(request.CountryCode))
//        {
//            companyCounts = await query
//                .Where(j => j.Company != null)
//                .GroupBy(j => j.Company!.CompanyName)
//                .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//                .OrderByDescending(c => c.Count)
//                .Take(50)
//                .ToListAsync(cancellationToken);

//            locationCounts = await query
//                .Where(j => j.Location != null)
//                .GroupBy(j => j.Location!.LocationName)
//                .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//                .OrderByDescending(c => c.Count)
//                .Take(50)
//                .ToListAsync(cancellationToken);
//        }
//        else
//        {
//            var countryCountQuery = BuildCountryCountQuery(request, fromDate);
//            countryCounts = await countryCountQuery
//                .GroupBy(j => j.Country.CountryCode)
//                .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//                .OrderByDescending(c => c.Count)
//                .Take(20) // Countries are limited, so smaller limit
//                .ToListAsync(cancellationToken);
//        }

//        return new FilterCountsResult
//        {
//            ContractTypeCounts = contractTypeCounts,
//            ContractTimeCounts = contractTimeCounts,
//            WorkLocationCounts = workLocationCounts,
//            SkillCounts = skillCounts,
//            LanguageCounts = languageCounts,
//            CompanyCounts = companyCounts,
//            LocationCounts = locationCounts,
//            CountryCounts = countryCounts
//        };
//    }

//    private IQueryable<JobPost> BuildCountryCountQuery(GetJobPostsQuery request, DateTime fromDate)
//    {
//        var query = _context.JobPosts
//            .AsNoTracking()
//            .Where(j => j.Created >= fromDate);

//        if (request.OnlyFavorites && !string.IsNullOrEmpty(request.UserId))
//            query = query.Where(j => j.UsersWhoFavorited.Any(u => u.Id == request.UserId));

//        if (!string.IsNullOrWhiteSpace(request.ContractType))
//            query = query.Where(j => j.ContractType != null && j.ContractType.Type == request.ContractType);

//        if (!string.IsNullOrWhiteSpace(request.ContractTime))
//            query = query.Where(j => j.ContractTime != null && j.ContractTime.Time == request.ContractTime);

//        if (!string.IsNullOrWhiteSpace(request.WorkLocation))
//            query = query.Where(j => j.WorkplaceModel != null && j.WorkplaceModel.Workplace == request.WorkLocation);

//        if (!string.IsNullOrWhiteSpace(request.Title))
//        {
//            var normalizedInput = request.Title.ToLowerInvariant()
//                .Replace(" ", "").Replace("-", "").Replace("_", "");
//            var pattern = $"%{normalizedInput}%";
//            query = query.Where(j => EF.Functions.Like(j.TitleNormalized, pattern));
//        }

//        if (request.Skills != null && request.Skills.Any())
//        {
//            foreach (var skill in request.Skills)
//            {
//                var skillLower = skill.ToLower();
//                query = query.Where(j => j.JobPostSkills.Any(js => js.Skill.SkillName.ToLower() == skillLower));
//            }
//        }

//        if (request.Languages != null && request.Languages.Any())
//        {
//            var requestedLanguagesLower = request.Languages.Select(l => l.ToLower()).ToList();
//            query = query.Where(j => j.JobPostLanguages.Any(jl => requestedLanguagesLower.Contains(jl.Language.Name.ToLower())));
//        }

//        return query;
//    }

//    private async Task<int> ComputeFavoriteCount(GetJobPostsQuery request, DateTime fromDate, CancellationToken cancellationToken)
//    {
//        if (string.IsNullOrEmpty(request.UserId))
//            return 0;

//        var favoritesQuery = BuildMainQuery(request, fromDate);
//        return await favoritesQuery
//            .Where(j => j.UsersWhoFavorited.Any(u => u.Id == request.UserId))
//            .CountAsync(cancellationToken);
//    }

//    private string GenerateFilterCacheKey(GetJobPostsQuery request, DateTime fromDate)
//    {
//        var keyParts = new List<string>
//        {
//            $"filters_{fromDate:yyyyMMddHH}", // Hour-based to handle time shifts
//            request.CountryCode ?? "all",
//            request.ContractType ?? "",
//            request.ContractTime ?? "",
//            request.WorkLocation ?? "",
//            request.Title ?? "",
//            request.Company ?? "",
//            request.Location ?? "",
//            string.Join(",", request.Skills ?? new List<string>()),
//            string.Join(",", request.Languages ?? new List<string>())
//        };

//        return string.Join("_", keyParts.Where(k => !string.IsNullOrEmpty(k)));
//    }

//    private class FilterCountsResult
//    {
//        public List<FilterCountDTO> ContractTypeCounts { get; set; } = new();
//        public List<FilterCountDTO> ContractTimeCounts { get; set; } = new();
//        public List<FilterCountDTO> WorkLocationCounts { get; set; } = new();
//        public List<FilterCountDTO> CompanyCounts { get; set; } = new();
//        public List<FilterCountDTO> LocationCounts { get; set; } = new();
//        public List<FilterCountDTO> SkillCounts { get; set; } = new();
//        public List<FilterCountDTO> LanguageCounts { get; set; } = new();
//        public List<FilterCountDTO> CountryCounts { get; set; } = new();
//    }
//}






















// Backup 2
//public class GetJobPostsQueryHandler : IRequestHandler<GetJobPostsQuery, JobPostPagedResultDTO>
//{
//    private readonly JobPostsDbContext _context;
//    private readonly IMemoryCache _cache;
//    private readonly JobPostAggregateService _aggregateService;
//    private readonly ILogger<GetJobPostsQueryHandler> _logger;

//    public GetJobPostsQueryHandler(
//        JobPostsDbContext context,
//        IMemoryCache cache,
//        JobPostAggregateService aggregateService,
//        ILogger<GetJobPostsQueryHandler> logger)
//    {
//        _context = context;
//        _cache = cache;
//        _aggregateService = aggregateService;
//        _logger = logger;
//    }

//    public async Task<JobPostPagedResultDTO> Handle(GetJobPostsQuery request, CancellationToken cancellationToken)
//    {
//        const int extendedTimeoutSeconds = 600;
//        var fromDate = DateTime.UtcNow.AddDays(-7 * request.TimeframeInWeeks);
//        int? previousTimeout = _context.Database.GetCommandTimeout();

//        try
//        {
//            _context.Database.SetCommandTimeout(extendedTimeoutSeconds);
//            var query = BuildMainQuery(request, fromDate);

//            var pageIds = await query
//                .OrderByDescending(j => j.Created)
//                .ThenBy(j => j.JobId)
//                .Select(j => j.JobId)
//                .Skip((request.Page - 1) * request.PageSize)
//                .Take(request.PageSize)
//                .ToListAsync(cancellationToken);

//            if (pageIds.Count == 0)
//                return new JobPostPagedResultDTO { Posts = new List<JobPostDTO>(), TotalCount = 0, TotalPages = 0 };

//            var totalCount = await query.CountAsync(cancellationToken);
//            var pageItems = await GetPageItems(pageIds, cancellationToken);
//            var posts = OrderPageItems(pageItems, pageIds);

//            // Get filter counts and country counts
//            var filterCounts = await GetFilterCounts(request, fromDate, cancellationToken);
//            var countryCounts = await GetCountryCounts(request.TimeframeInWeeks, cancellationToken);
//            var favoriteCount = await ComputeFavoriteCount(request, fromDate, cancellationToken);

//            return new JobPostPagedResultDTO
//            {
//                Posts = posts,
//                TotalCount = totalCount,
//                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
//                ContractTypeCounts = filterCounts.ContractTypeCounts,
//                ContractTimeCounts = filterCounts.ContractTimeCounts,
//                WorkLocationCounts = filterCounts.WorkLocationCounts,
//                CompanyCounts = filterCounts.CompanyCounts,
//                LocationCounts = filterCounts.LocationCounts,
//                SkillCounts = filterCounts.SkillCounts,
//                LanguageCounts = filterCounts.LanguageCounts,
//                CountryCounts = countryCounts,
//                FavoriteCount = favoriteCount
//            };
//        }
//        finally
//        {
//            _context.Database.SetCommandTimeout(previousTimeout);
//        }
//    }

//    // NEW METHOD: Get country counts from aggregates
//    private async Task<List<FilterCountDTO>> GetCountryCounts(int timeframeInWeeks, CancellationToken cancellationToken)
//    {
//        var cacheKey = $"country_counts_{timeframeInWeeks}";

//        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
//        {
//            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

//            var fromDate = DateTime.UtcNow.AddDays(-7 * timeframeInWeeks);
//            var calculationDate = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, fromDate.Hour, 0, 0, DateTimeKind.Utc);

//            // Try to get from aggregates first
//            var aggregateCounts = await _context.JobPostAggregates
//                .AsNoTracking()
//                .Where(a => a.TimeframeInWeeks == timeframeInWeeks &&
//                           a.CalculationDate == calculationDate &&
//                           a.LastUpdated > DateTime.UtcNow.AddHours(-2))
//                .GroupBy(a => a.CountryCode)
//                .Select(g => new FilterCountDTO
//                {
//                    Key = g.Key,
//                    Count = g.Sum(a => a.TotalJobs)
//                })
//                .OrderByDescending(c => c.Count)
//                .ToListAsync(cancellationToken);

//            // If no fresh aggregates, fallback to real-time computation
//            if (!aggregateCounts.Any())
//            {
//                aggregateCounts = await _context.JobPosts
//                    .AsNoTracking()
//                    .Where(j => j.Created >= fromDate)
//                    .GroupBy(j => j.Country.CountryCode)
//                    .Select(g => new FilterCountDTO
//                    {
//                        Key = g.Key,
//                        Count = g.Count()
//                    })
//                    .OrderByDescending(c => c.Count)
//                    .ToListAsync(cancellationToken);
//            }

//            return aggregateCounts;
//        });
//    }

//    private async Task<FilterCountsResult> GetFilterCounts(GetJobPostsQuery request, DateTime fromDate, CancellationToken cancellationToken)
//    {
//        var canUseAggregates = CanUsePrecomputedAggregates(request);

//        if (canUseAggregates && !string.IsNullOrWhiteSpace(request.CountryCode))
//        {
//            var calculationDate = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, fromDate.Hour, 0, 0, DateTimeKind.Utc);

//            var aggregate = await _context.JobPostAggregates
//                .AsNoTracking()
//                .FirstOrDefaultAsync(a => a.CountryCode == request.CountryCode &&
//                                        a.TimeframeInWeeks == request.TimeframeInWeeks &&
//                                        a.CalculationDate == calculationDate,
//                                        cancellationToken);

//            if (aggregate != null && aggregate.LastUpdated > DateTime.UtcNow.AddHours(-2))
//            {
//                // Use aggregates for stable counts, compute volatile ones on-demand
//                var query = BuildMainQuery(request, fromDate);

//                // Compute volatile counts with caching
//                var volatileCountsKey = $"volatile_counts_{request.CountryCode}_{fromDate:yyyyMMddHH}";
//                var volatileCounts = await _cache.GetOrCreateAsync(volatileCountsKey, async entry =>
//                {
//                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2);

//                    var companyCounts = await query
//                        .Where(j => j.Company != null)
//                        .GroupBy(j => j.Company!.CompanyName)
//                        .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//                        .OrderByDescending(c => c.Count)
//                        .ToListAsync(cancellationToken);

//                    var locationCounts = await query
//                        .Where(j => j.Location != null)
//                        .GroupBy(j => j.Location!.LocationName)
//                        .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//                        .OrderByDescending(c => c.Count)
//                        .ToListAsync(cancellationToken);

//                    var skillCounts = await query
//                        .SelectMany(j => j.JobPostSkills)
//                        .GroupBy(js => js.Skill.SkillName)
//                        .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//                        .OrderByDescending(c => c.Count)
//                        .ToListAsync(cancellationToken);

//                    var languageCounts = await query
//                        .SelectMany(j => j.JobPostLanguages)
//                        .GroupBy(jl => jl.Language.Name)
//                        .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//                        .OrderByDescending(c => c.Count)
//                        .Take(50)
//                        .ToListAsync(cancellationToken);

//                    return new
//                    {
//                        CompanyCounts = companyCounts,
//                        LocationCounts = locationCounts,
//                        SkillCounts = skillCounts,
//                        LanguageCounts = languageCounts
//                    };
//                });

//                return new FilterCountsResult
//                {
//                    // From aggregates (fast)
//                    ContractTypeCounts = aggregate.ContractTypeCounts,
//                    ContractTimeCounts = aggregate.ContractTimeCounts,
//                    WorkLocationCounts = aggregate.WorkLocationCounts,
//                    // From cache (medium speed)
//                    CompanyCounts = volatileCounts.CompanyCounts,
//                    LocationCounts = volatileCounts.LocationCounts,
//                    SkillCounts = volatileCounts.SkillCounts,
//                    LanguageCounts = volatileCounts.LanguageCounts,
//                    CountryCounts = new List<FilterCountDTO>() // Not used in this path
//                };
//            }
//        }

//        // Fallback to full real-time computation
//        return await ComputeFilterCounts(request, fromDate, cancellationToken);
//    }

//    private static bool CanUsePrecomputedAggregates(GetJobPostsQuery request)
//    {
//        // Only use aggregates when we have minimal filtering (just country and timeframe)
//        return string.IsNullOrWhiteSpace(request.ContractType) &&
//               string.IsNullOrWhiteSpace(request.ContractTime) &&
//               string.IsNullOrWhiteSpace(request.WorkLocation) &&
//               string.IsNullOrWhiteSpace(request.Title) &&
//               string.IsNullOrWhiteSpace(request.Company) &&
//               string.IsNullOrWhiteSpace(request.Location) &&
//               (request.Skills == null || !request.Skills.Any()) &&
//               (request.Languages == null || !request.Languages.Any()) &&
//               !request.OnlyFavorites;
//    }

//    private IQueryable<JobPost> BuildMainQuery(GetJobPostsQuery request, DateTime fromDate)
//    {
//        var query = _context.JobPosts
//            .AsNoTracking()
//            .Where(j => j.Created >= fromDate);

//        if (!string.IsNullOrWhiteSpace(request.CountryCode))
//            query = query.Where(j => j.Country.CountryCode == request.CountryCode);

//        if (request.OnlyFavorites && !string.IsNullOrEmpty(request.UserId))
//            query = query.Where(j => j.UsersWhoFavorited.Any(u => u.Id == request.UserId));

//        if (!string.IsNullOrWhiteSpace(request.ContractType))
//            query = query.Where(j => j.ContractType != null && j.ContractType.Type == request.ContractType);

//        if (!string.IsNullOrWhiteSpace(request.ContractTime))
//            query = query.Where(j => j.ContractTime != null && j.ContractTime.Time == request.ContractTime);

//        if (!string.IsNullOrWhiteSpace(request.WorkLocation))
//            query = query.Where(j => j.WorkplaceModel != null && j.WorkplaceModel.Workplace == request.WorkLocation);

//        if (!string.IsNullOrWhiteSpace(request.Title))
//        {
//            var normalizedInput = request.Title.ToLowerInvariant()
//                .Replace(" ", "").Replace("-", "").Replace("_", "");
//            var pattern = $"%{normalizedInput}%";
//            query = query.Where(j => EF.Functions.Like(j.TitleNormalized, pattern));
//        }

//        // Company/Location filtering only available when a specific country is selected
//        // This prevents performance issues and improves UX when browsing all countries
//        if (!string.IsNullOrWhiteSpace(request.CountryCode))
//        {
//            if (!string.IsNullOrWhiteSpace(request.Company))
//            {
//                query = query.Where(j => j.Company != null &&
//                    EF.Functions.Collate(j.Company.CompanyName!, "Latin1_General_CI_AI").Contains(request.Company));
//            }

//            if (!string.IsNullOrWhiteSpace(request.Location))
//            {
//                query = query.Where(j => j.Location != null &&
//                    EF.Functions.Collate(j.Location.LocationName!, "Latin1_General_CI_AI").Contains(request.Location));
//            }
//        }

//        if (request.Skills != null && request.Skills.Any())
//        {
//            foreach (var skill in request.Skills)
//            {
//                var skillLower = skill.ToLower();
//                query = query.Where(j => j.JobPostSkills.Any(js => js.Skill.SkillName.ToLower() == skillLower));
//            }
//        }

//        if (request.Languages != null && request.Languages.Any())
//        {
//            var requestedLanguagesLower = request.Languages.Select(l => l.ToLower()).ToList();
//            query = query.Where(j => j.JobPostLanguages.Any(jl => requestedLanguagesLower.Contains(jl.Language.Name.ToLower())));
//        }

//        return query;
//    }

//    private async Task<List<JobPostDTO>> GetPageItems(List<long> pageIds, CancellationToken cancellationToken)
//    {
//        return await _context.JobPosts
//            .AsNoTracking()
//            .AsSplitQuery() // This tells EF to split the query for better performance
//            .Where(j => pageIds.Contains(j.JobId))
//            .Select(j => new JobPostDTO
//            {
//                JobId = j.JobId,
//                Title = j.Title,
//                Description = j.Description,
//                Url = j.Url,
//                SalaryMin = j.SalaryMin,
//                SalaryMax = j.SalaryMax,
//                Created = j.Created,
//                CountryName = j.Country.CountryName,
//                CompanyName = j.Company != null ? j.Company.CompanyName : null,
//                LocationName = j.Location != null ? j.Location.LocationName : null,
//                ContractType = j.ContractType != null ? j.ContractType.Type : null,
//                ContractTime = j.ContractTime != null ? j.ContractTime.Time : null,
//                WorkLocation = j.WorkplaceModel != null ? j.WorkplaceModel.Workplace : null,
//                Skills = j.JobPostSkills.Select(js => js.Skill.SkillName).ToList(),
//                Languages = j.JobPostLanguages.Select(jl => jl.Language.Name).ToList()
//            })
//            .ToListAsync(cancellationToken);
//    }

//    private static List<JobPostDTO> OrderPageItems(List<JobPostDTO> pageItems, List<long> pageIds)
//    {
//        var pos = pageIds.Select((id, i) => new { id, i }).ToDictionary(x => x.id, x => x.i);
//        return pageItems.OrderBy(p => pos[p.JobId]).ToList();
//    }

//    private async Task<FilterCountsResult> ComputeFilterCounts(GetJobPostsQuery request, DateTime fromDate, CancellationToken cancellationToken)
//    {
//        var query = BuildMainQuery(request, fromDate);

//        var contractTypeCounts = await query
//            .Where(j => j.ContractType != null)
//            .GroupBy(j => j.ContractType!.Type)
//            .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//            .OrderByDescending(c => c.Count)
//            .ToListAsync(cancellationToken);

//        var contractTimeCounts = await query
//            .Where(j => j.ContractTime != null)
//            .GroupBy(j => j.ContractTime!.Time)
//            .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//            .OrderByDescending(c => c.Count)
//            .ToListAsync(cancellationToken);

//        var workLocationCounts = await query
//            .Where(j => j.WorkplaceModel != null)
//            .GroupBy(j => j.WorkplaceModel!.Workplace)
//            .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//            .OrderByDescending(c => c.Count)
//            .ToListAsync(cancellationToken);

//        var skillCounts = await query
//            .SelectMany(j => j.JobPostSkills)
//            .GroupBy(js => js.Skill.SkillName)
//            .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//            .OrderByDescending(c => c.Count)
//            .ToListAsync(cancellationToken);

//        var languageCounts = await query
//            .SelectMany(j => j.JobPostLanguages)
//            .GroupBy(jl => jl.Language.Name)
//            .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//            .OrderByDescending(c => c.Count)
//            .Take(50)
//            .ToListAsync(cancellationToken);

//        var companyCounts = new List<FilterCountDTO>();
//        var locationCounts = new List<FilterCountDTO>();

//        // Company/Location counts only computed when a specific country is selected
//        // This prevents performance issues when browsing all countries
//        if (!string.IsNullOrWhiteSpace(request.CountryCode))
//        {
//            companyCounts = await query
//                .Where(j => j.Company != null)
//                .GroupBy(j => j.Company!.CompanyName)
//                .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//                .OrderByDescending(c => c.Count)
//                .ToListAsync(cancellationToken);

//            locationCounts = await query
//                .Where(j => j.Location != null)
//                .GroupBy(j => j.Location!.LocationName)
//                .Select(g => new FilterCountDTO { Key = g.Key!, Count = g.Count() })
//                .OrderByDescending(c => c.Count)
//                .ToListAsync(cancellationToken);
//        }

//        return new FilterCountsResult
//        {
//            ContractTypeCounts = contractTypeCounts,
//            ContractTimeCounts = contractTimeCounts,
//            WorkLocationCounts = workLocationCounts,
//            SkillCounts = skillCounts,
//            LanguageCounts = languageCounts,
//            CompanyCounts = companyCounts,
//            LocationCounts = locationCounts,
//            CountryCounts = new List<FilterCountDTO>() // Not used in fallback computation
//        };
//    }

//    private async Task<int> ComputeFavoriteCount(GetJobPostsQuery request, DateTime fromDate, CancellationToken cancellationToken)
//    {
//        if (string.IsNullOrEmpty(request.UserId))
//            return 0;

//        var favoritesQuery = BuildMainQuery(request, fromDate);
//        return await favoritesQuery
//            .Where(j => j.UsersWhoFavorited.Any(u => u.Id == request.UserId))
//            .CountAsync(cancellationToken);
//    }
//}

