using JobPosts.Commands.JobPosts;
using JobPosts.Data;
using JobPosts.DTOs.JobPosts;
using JobPosts.Mappers;
using JobPosts.Providers;
using JobPosts.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
using System.Text.Json;

namespace JobPosts.Handlers.JobPosts
{
    public class FetchAdzunaJobsHandler : IRequestHandler<FetchAdzunaJobsCommand, FetchResultsDTO>
    {
        private readonly JobPostsDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly IAdzunaCredentialProvider _credProvider;
        private readonly ILogger<FetchAdzunaJobsHandler> _logger;

        public FetchAdzunaJobsHandler(
            JobPostsDbContext context,
            HttpClient httpClient,
            IAdzunaCredentialProvider credProvider,
            CacheManagementService cacheManagementService,
            ILogger<FetchAdzunaJobsHandler> logger)
        {
            _context = context;
            _httpClient = httpClient;
            _credProvider = credProvider;
            _logger = logger;
        }

        public async Task<FetchResultsDTO> Handle(
            FetchAdzunaJobsCommand request,
            CancellationToken cancellationToken)
        {
            const int resultsPerPage = 50, maxRetries = 3, retryDelaySec = 5;

            int page = 1,
                totalJobs = 0,
                savedCount = 0,
                totalPages = 0,
                consecutiveNoInsert = 0;

            var countryCode = request.CountryCode.ToLowerInvariant();

            _logger.LogInformation("\n\t\t-> Starting job fetch for country: [{CountryCode}]", countryCode.ToUpper());

            try
            {
                while (true)
                {
                    var cred = _credProvider.GetNextCredential();
                    if (cred == null)
                    {
                        _logger.LogWarning("\n\t\t-> All credentials exhausted or blocked. Stopping fetch for [{CountryCode}]", countryCode.ToUpper());
                        break;
                    }

                    _logger.LogDebug("\n\t\t-> Fetching page [{Page}] for {CountryCode} using AppId [{AppId}]",
                        page, countryCode.ToUpper(), cred.AppId);

                    var builder = new UriBuilder(
                        $"https://api.adzuna.com/v1/api/jobs/{countryCode}/search/{page}")
                    {
                        Query = string.Join("&", new Dictionary<string, string>
                        {
                            ["app_id"] = cred.AppId,
                            ["app_key"] = cred.AppKey,
                            ["category"] = "it-jobs",
                            ["results_per_page"] = resultsPerPage.ToString(),
                            ["sort_by"] = "date"
                        }.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"))
                    };

                    HttpResponseMessage? response = null;
                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            response = await _httpClient
                                .SendAsync(new HttpRequestMessage(HttpMethod.Get, builder.Uri),
                                           HttpCompletionOption.ResponseHeadersRead,
                                           cancellationToken);

                            if (response.StatusCode == HttpStatusCode.BadGateway && attempt < maxRetries)
                            {
                                _logger.LogWarning("\n\t\t-> 502 Bad Gateway - Attempt [{Attempt}]. Retrying in [{DelaySec}]s...",
                                    attempt, retryDelaySec);
                                await Task.Delay(TimeSpan.FromSeconds(retryDelaySec), cancellationToken);
                                continue;
                            }

                            response.EnsureSuccessStatusCode();
                            break;
                        }
                        catch (Exception ex) when (attempt < maxRetries)
                        {
                            _logger.LogWarning(ex, "\n\t\t-> Attempt [{Attempt}] failed. Retrying...", attempt);
                            await Task.Delay(TimeSpan.FromSeconds(retryDelaySec), cancellationToken);
                        }
                    }

                    if (response == null)
                    {
                        _logger.LogError("\n\t\t-> All retry attempts failed for [{CountryCode}]. Marking credential as exhausted",
                            countryCode.ToUpper());
                        _credProvider.MarkCredentialAsExhausted(cred);
                        continue;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    var json = Encoding.UTF8.GetString(bytes);

                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (page == 1)
                    {
                        totalJobs = root.GetProperty("count").GetInt32();
                        totalPages = (int)Math.Ceiling(totalJobs / (double)resultsPerPage);
                        _logger.LogInformation("\n\t\t-> Total jobs available for [{CountryCode}]: [{TotalJobs}] across [{TotalPages}] pages",
                            countryCode.ToUpper(), totalJobs, totalPages);
                    }

                    var fetched = await AdzunaJobPostMapper
                        .MapToJobPostsAsync(doc, countryCode, _context);

                    var fetchedIds = fetched.Select(j => j.JobId).ToArray();
                    var existingIds = await _context.JobPosts
                        .Where(j => fetchedIds.Contains(j.JobId))
                        .Select(j => j.JobId)
                        .ToListAsync(cancellationToken);

                    var toInsert = fetched.Where(j => !existingIds.Contains(j.JobId)).ToList();

                    _logger.LogDebug("\n\t\t-> Page [{Page}] for [{CountryCode}] - fetched: [{FetchedCount}], duplicates: [{DuplicateCount}], new: [{NewCount}]",
                        page, countryCode.ToUpper(), fetched.Count, existingIds.Count, toInsert.Count);

                    if (toInsert.Count == 0)
                    {
                        consecutiveNoInsert++;
                        if (consecutiveNoInsert >= 3)
                        {
                            _logger.LogInformation("\n\t\t-> No new jobs inserted for 3 consecutive pages. Stopping fetch early for [{CountryCode}]",
                                countryCode.ToUpper());
                            break;
                        }
                    }
                    else
                    {
                        consecutiveNoInsert = 0;
                        _context.JobPosts.AddRange(toInsert);
                        await _context.SaveChangesAsync(cancellationToken);
                        savedCount += toInsert.Count;
                    }

                    if (page >= totalPages)
                        break;

                    page++;
                    await Task.Delay(500, cancellationToken);
                }

                var result = new FetchResultsDTO
                {
                    TotalJobs = totalJobs,
                    TotalPages = totalPages,
                    SavedJobs = savedCount
                };

                _logger.LogInformation("\n\t\t-> Fetch completed for [{CountryCode}]: Pages=[{TotalPages}], TotalJobs=[{TotalJobs}], Saved=[{SavedCount}]",
                    countryCode.ToUpper(), totalPages, totalJobs, savedCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "\n\t\t-> Error during fetch operation for [{CountryCode}]", countryCode.ToUpper());
                throw;
            }
        }
    }
}
