using JobPosts.Commands.JobPosts;
using JobPosts.DTOs;
using JobPosts.DTOs.JobPosts;
using JobPosts.Queries.JobPosts;
using JobPosts.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace JobPosts.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdzunaController : ControllerBase
    {
        private readonly AdzunaService _adzunaService;
        private readonly IMediator _mediator;
        private readonly CacheInvalidationService _cacheInvalidationService;
        private readonly BackgroundCacheWarmupService _backgroundCacheService;
        private readonly ILogger<AdzunaController> _logger;

        public AdzunaController(
            AdzunaService adzunaService,
            IMediator mediator,
            CacheInvalidationService cacheInvalidationService,
            BackgroundCacheWarmupService backgroundCacheService,
            ILogger<AdzunaController> logger)
        {
            _adzunaService = adzunaService;
            _mediator = mediator;
            _cacheInvalidationService = cacheInvalidationService;
            _backgroundCacheService = backgroundCacheService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves and saves unique data from Adzuna API. Country codes: de, gb, us, nl, be, at, ch
        /// </summary>
        [Authorize]
        [HttpPost("fetch")]
        public async Task<IActionResult> Fetch([FromQuery] string country)
        {
            var command = new FetchAdzunaJobsCommand(country);
            var result = await _mediator.Send(command);

            var response = new
            {
                Country = country.ToUpper(),
                TotalJobs = result.TotalJobs,
                TotalPages = result.TotalPages,
                SavedJobs = result.SavedJobs,
                CacheRefreshed = false,
                CacheWarmupQueued = ""
            };

            // Add cache management for individual country fetch
            if (result.SavedJobs > 0)
            {
                var normalizedCountry = country.ToUpper();

                // Immediately invalidate country-specific and global cache
                _cacheInvalidationService.InvalidateCountrySpecificCaches(normalizedCountry);
                _cacheInvalidationService.InvalidateCountrySpecificCaches(""); // Global cache

                // Queue background cache warmup
                _backgroundCacheService.QueueCacheWarmup(normalizedCountry);
                _backgroundCacheService.QueueCacheWarmup(""); // Global cache

                _logger.LogInformation("Cache warmup queued for countries: [{Country}] + GLOBAL", normalizedCountry);

                response = response with
                {
                    CacheRefreshed = true,
                    CacheWarmupQueued = $"{normalizedCountry} + GLOBAL"
                };
            }

            return Ok(response);
        }

        [Authorize]
        [HttpGet("job-details/{id}")]
        public async Task<IActionResult> GetJobPostById(int id)
        {
            var result = await _mediator.Send(new GetJobPostByIdQuery(id));
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [Authorize]
        [HttpGet("get-job-posts")]
        public async Task<IActionResult> GetJobPostsAsync(
            [FromQuery] string? countryCode,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int timeframeInWeeks = 1,
            [FromQuery] string? contractType = null,
            [FromQuery] string? contractTime = null,
            [FromQuery] string? workLocation = null,
            [FromQuery] string? title = null,
            [FromQuery] string? location = null,
            [FromQuery] string? company = null,
            [FromQuery] List<string>? skills = null,
            [FromQuery] List<string>? languages = null,
            [FromQuery] bool onlyFavorites = false)
        {
            var ct = HttpContext.RequestAborted;
            var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            timeframeInWeeks = Math.Max(1, timeframeInWeeks);

            string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            countryCode = Norm(countryCode);
            contractType = Norm(contractType);
            contractTime = Norm(contractTime);
            workLocation = Norm(workLocation);
            title = Norm(title);
            location = Norm(location);
            company = Norm(company);

            var query = new GetJobPostsQuery
            {
                CountryCode = countryCode,
                Page = page,
                PageSize = pageSize,
                TimeframeInWeeks = timeframeInWeeks,
                ContractType = contractType,
                ContractTime = contractTime,
                WorkLocation = workLocation,
                Title = title,
                Location = location,
                Company = company,
                Skills = skills,
                Languages = languages,
                OnlyFavorites = onlyFavorites,
                UserId = userId
            };

            var result = await _mediator.Send(query, ct);
            Response.Headers["Cache-Control"] = "no-store";
            return Ok(result);
        }

        [Authorize]
        [HttpGet("filter-options")]
        public async Task<IActionResult> GetFilterOptionsAsync(
            [FromQuery] string? countryCode = null,
            [FromQuery] int timeframeInWeeks = 1)
        {
            try
            {
                var query = new GetFilterOptionsQuery
                {
                    CountryCode = countryCode,
                    TimeframeInWeeks = timeframeInWeeks
                };

                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filter options for country: {CountryCode}", countryCode);

                return Ok(new FilterOptionsDTO());
            }
        }

        [HttpGet("contract-types")]
        public async Task<IActionResult> GetContractTypesAsync(
            [FromQuery] string? countryCode = null,
            [FromQuery] int timeframeInWeeks = 1)
        {
            try
            {
                var query = new GetContractTypesQuery
                {
                    CountryCode = countryCode,
                    TimeframeInWeeks = timeframeInWeeks
                };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contract types for country: {CountryCode}", countryCode);
                return Ok(new List<string>());
            }
        }

        [HttpGet("contract-times")]
        public async Task<IActionResult> GetContractTimesAsync(
            [FromQuery] string? countryCode = null,
            [FromQuery] int timeframeInWeeks = 1)
        {
            try
            {
                var query = new GetContractTimesQuery
                {
                    CountryCode = countryCode,
                    TimeframeInWeeks = timeframeInWeeks
                };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contract times for country: {CountryCode}", countryCode);
                return Ok(new List<string>());
            }
        }

        [HttpGet("work-locations")]
        public async Task<IActionResult> GetWorkLocationsAsync(
            [FromQuery] string? countryCode = null,
            [FromQuery] int timeframeInWeeks = 1)
        {
            try
            {
                var query = new GetWorkLocationsQuery
                {
                    CountryCode = countryCode,
                    TimeframeInWeeks = timeframeInWeeks
                };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting work locations for country: {CountryCode}", countryCode);
                return Ok(new List<string>());
            }
        }

        [Authorize]
        [HttpGet("companies")]
        public async Task<IActionResult> GetCompaniesAsync(
            [FromQuery] string? countryCode = null,
            [FromQuery] int timeframeInWeeks = 1)
        {
            try
            {
                var query = new GetCompaniesQuery
                {
                    CountryCode = countryCode,
                    TimeframeInWeeks = timeframeInWeeks
                };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting companies for country: {CountryCode}", countryCode);
                return Ok(new List<string>());
            }
        }

        [Authorize]
        [HttpGet("locations")]
        public async Task<IActionResult> GetLocationsAsync(
            [FromQuery] string? countryCode = null,
            [FromQuery] int timeframeInWeeks = 1)
        {
            try
            {
                var query = new GetLocationsQuery
                {
                    CountryCode = countryCode,
                    TimeframeInWeeks = timeframeInWeeks
                };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting locations for country: {CountryCode}", countryCode);
                return Ok(new List<string>());
            }
        }

        [Authorize]
        [HttpGet("skills")]
        public async Task<IActionResult> GetSkillsAsync(
            [FromQuery] string? countryCode = null,
            [FromQuery] int timeframeInWeeks = 1)
        {
            try
            {
                var query = new GetSkillsQuery
                {
                    CountryCode = countryCode,
                    TimeframeInWeeks = timeframeInWeeks
                };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting skills for country: {CountryCode}", countryCode);
                return Ok(new List<string>());
            }
        }

        [Authorize]
        [HttpGet("languages")]
        public async Task<IActionResult> GetLanguagesAsync(
            [FromQuery] string? countryCode = null,
            [FromQuery] int timeframeInWeeks = 1)
        {
            try
            {
                var query = new GetLanguagesQuery
                {
                    CountryCode = countryCode,
                    TimeframeInWeeks = timeframeInWeeks
                };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting languages for country: {CountryCode}", countryCode);
                return Ok(new List<string>());
            }
        }

        [HttpGet("countries")]
        public async Task<IActionResult> GetCountriesAsync()
        {
            try
            {
                var query = new GetCountriesQuery();
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting countries");
                return Ok(new List<string>());
            }
        }

        [Authorize]
        [HttpGet("export-job-posts-json")]
        public async Task<IActionResult> ExportJobPostsJsonAsync(
            [FromQuery] string? countryCode = null,
            [FromQuery] int timeframeInWeeks = 1,
            [FromQuery] string? contractType = null,
            [FromQuery] string? contractTime = null,
            [FromQuery] string? workLocation = null,
            [FromQuery] string? title = null,
            [FromQuery] string? location = null,
            [FromQuery] string? company = null,
            [FromQuery] List<string>? skills = null,
            [FromQuery] List<string>? languages = null,
            [FromQuery] bool onlyFavorites = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = new GetJobPostsQuery
            {
                CountryCode = countryCode, // Will be null for all countries
                Page = 1,
                PageSize = int.MaxValue,
                TimeframeInWeeks = timeframeInWeeks,
                ContractType = contractType,
                ContractTime = contractTime,
                WorkLocation = workLocation,
                Title = title,
                Location = location,
                Company = company,
                Skills = skills,
                Languages = languages,
                OnlyFavorites = onlyFavorites,
                UserId = userId
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

            var result = await _mediator.Send(query, cts.Token);

            var exportData = new
            {
                ExportMetadata = new
                {
                    ExportDate = DateTime.UtcNow,
                    TotalRecords = result.TotalCount,
                    Filters = new
                    {
                        CountryCode = countryCode ?? "ALL", // Show "ALL" when null
                        TimeframeInWeeks = timeframeInWeeks,
                        ContractType = contractType,
                        ContractTime = contractTime,
                        WorkLocation = workLocation,
                        Title = title,
                        Location = location,
                        Company = company,
                        Skills = skills,
                        Languages = languages,
                        OnlyFavorites = onlyFavorites
                    }
                },
                JobPosts = result.Posts
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                // This disables escaping for Unicode chars like ö, ü, etc.
            });

            var fileName = $"JobPosts_{countryCode ?? "ALL"}_{DateTime.UtcNow:yyyyMMddHHmmss}.json";

            // Specify charset=utf-8 explicitly in content type:
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", fileName);
        }

        [Authorize]
        [HttpGet("export-job-posts-csv")]
        public async Task<IActionResult> ExportJobPostsCsvAsync(
            [FromQuery] string? countryCode = null,
            [FromQuery] int timeframeInWeeks = 1,
            [FromQuery] string? contractType = null,
            [FromQuery] string? contractTime = null,
            [FromQuery] string? workLocation = null,
            [FromQuery] string? title = null,
            [FromQuery] string? location = null,
            [FromQuery] string? company = null,
            [FromQuery] List<string>? skills = null,
            [FromQuery] List<string>? languages = null,
            [FromQuery] bool onlyFavorites = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var query = new GetJobPostsQuery
            {
                CountryCode = countryCode,
                Page = 1,
                PageSize = int.MaxValue,
                TimeframeInWeeks = timeframeInWeeks,
                ContractType = contractType,
                ContractTime = contractTime,
                WorkLocation = workLocation,
                Title = title,
                Location = location,
                Company = company,
                Skills = skills,
                Languages = languages,
                OnlyFavorites = onlyFavorites,
                UserId = userId
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var result = await _mediator.Send(query, cts.Token);

            var csvData = new StringBuilder();

            // Add metadata as header comments
            csvData.AppendLine($"# Export Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            csvData.AppendLine($"# Total Records: {result.TotalCount}");
            csvData.AppendLine($"# Country Code: {countryCode ?? "ALL"}");
            if (!string.IsNullOrEmpty(contractType)) csvData.AppendLine($"# Contract Type: {contractType}");
            if (!string.IsNullOrEmpty(contractTime)) csvData.AppendLine($"# Contract Time: {contractTime}");
            if (!string.IsNullOrEmpty(workLocation)) csvData.AppendLine($"# Work Location: {workLocation}");
            if (!string.IsNullOrEmpty(title)) csvData.AppendLine($"# Title Filter: {title}");
            if (!string.IsNullOrEmpty(location)) csvData.AppendLine($"# Location Filter: {location}");
            if (!string.IsNullOrEmpty(company)) csvData.AppendLine($"# Company Filter: {company}");
            if (skills?.Any() == true) csvData.AppendLine($"# Skills Filter: {string.Join(", ", skills)}");
            if (languages?.Any() == true) csvData.AppendLine($"# Languages Filter: {string.Join(", ", languages)}");
            if (onlyFavorites) csvData.AppendLine("# Only Favorites: true");
            csvData.AppendLine();

            // CSV Headers (only the required columns)
            csvData.AppendLine("JobId,Title,Url,CompanyName,CompanyUrl,LocationName,CountryName");

            // CSV Rows
            foreach (var jobPost in result.Posts)
            {
                var row = new List<string>
        {
            jobPost.Id.ToString(),
            EscapeCsvField(jobPost.Title),
            EscapeCsvField(jobPost.Url),
            EscapeCsvField(jobPost.CompanyName),
            EscapeCsvField(jobPost.CompanyUrl),
            EscapeCsvField(jobPost.LocationName),
            EscapeCsvField(jobPost.CountryName)
        };

                csvData.AppendLine(string.Join(",", row));
            }

            var fileName = $"JobPosts_{countryCode ?? "ALL"}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            // Return CSV with UTF-8 BOM for Excel compatibility
            var csvBytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(csvData.ToString()))
                .ToArray();

            return File(csvBytes, "text/csv; charset=utf-8", fileName);
        }

        // Helper method
        private static string EscapeCsvField(string? field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            if (field.Contains(',') || field.Contains('\n') || field.Contains('\r') || field.Contains('"'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }

            return field;
        }

        [Authorize]
        [HttpGet("get-job-posts-with-coordinates")]
        public async Task<IActionResult> GetJobPostsWithCoordinatesAsync(
            [FromQuery] string countryCode,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int timeframeInWeeks = 1,
            [FromQuery] string? contractType = null,
            [FromQuery] string? contractTime = null,
            [FromQuery] string? workLocation = null,
            [FromQuery] string? title = null,
            [FromQuery] string? location = null,
            [FromQuery] string? company = null,
            [FromQuery] List<string>? skills = null,
            [FromQuery] List<string>? languages = null,
            [FromQuery] int? locationId = null,
            [FromQuery] bool groupByLocation = true,
            [FromQuery] bool summaryMode = false,
            [FromQuery] bool getAll = false)
        {
            var query = new GetJobPostsWithCoordinatesQuery
            {
                CountryCode = countryCode,
                Page = page,
                PageSize = pageSize,
                TimeframeInWeeks = timeframeInWeeks,
                ContractType = contractType,
                ContractTime = contractTime,
                WorkLocation = workLocation,
                Title = title,
                Location = location,
                Company = company,
                Skills = skills,
                Languages = languages,
                LocationId = locationId,
                GroupByLocation = groupByLocation,
                SummaryMode = summaryMode,
                GetAll = getAll
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("get-posts-by-location-id")]
            public async Task<IActionResult> GetPostsByLocationIdAsync(
            [FromQuery] int locationId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int timeframeInWeeks = 1,
            [FromQuery] string? contractType = null,
            [FromQuery] string? contractTime = null,
            [FromQuery] string? workLocation = null,
            [FromQuery] string? title = null,
            [FromQuery] string? company = null,
            [FromQuery] List<string>? skills = null,
            [FromQuery] List<string>? languages = null)
        {
            var query = new GetPostsByLocationIdQuery
            {
                Page = page,
                PageSize = pageSize,
                TimeframeInWeeks = timeframeInWeeks,
                ContractType = contractType,
                ContractTime = contractTime,
                WorkLocation = workLocation,
                Title = title,
                Company = company,
                Skills = skills,
                Languages = languages,
                LocationId = locationId
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
    }
}
