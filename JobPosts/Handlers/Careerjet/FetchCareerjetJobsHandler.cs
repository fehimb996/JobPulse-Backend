using JobPosts.Commands.Careerjet;
using JobPosts.Commands.JobPosts;
using JobPosts.Data;
using JobPosts.DTOs.JobPosts;
using JobPosts.Mappers;
using JobPosts.Models;
using JobPosts.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace JobPosts.Handlers.Careerjet
{
    public class FetchCareerjetJobsHandler : IRequestHandler<FetchCareerjetJobsCommand, FetchResultsDTO>
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly HttpClient _httpClient;
        private readonly ILogger<FetchCareerjetJobsHandler> _logger;
        private readonly string _apiKey;
        private readonly string _userIp;
        private readonly string _userAgent;
        private const int ResultsPerPage = 50;
        private const int DelayMs = 500;
        private const int UrlResolveConcurrency = 3;
        private const int MaxRedirects = 15;
        private const int UrlResolveTimeoutSeconds = 15;

        public FetchCareerjetJobsHandler(
            IServiceScopeFactory serviceScopeFactory,
            HttpClient httpClient,
            CacheManagementService cacheManagementService,
            IConfiguration configuration,
            ILogger<FetchCareerjetJobsHandler> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["Careerjet:AffId"] ?? throw new InvalidOperationException("Careerjet AffId missing");
            _userIp = configuration["Careerjet:UserIp"] ?? "185.56.251.220";
            _userAgent = configuration["Careerjet:UserAgent"] ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";
        }

        public async Task<FetchResultsDTO> Handle(
            FetchCareerjetJobsCommand request,
            CancellationToken cancellationToken)
        {
            var countryCode = request.CountryCode.ToUpperInvariant();
            var locale = MapCountryCodeToLocale(countryCode);
            _logger.LogInformation("Starting Careerjet job fetch for country: {CountryCode}", countryCode);

            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<JobPostsDbContext>();
                var country = await context.Countries
                    .FirstOrDefaultAsync(c => c.CountryCode.ToLower() == countryCode.ToLower(), cancellationToken);

                if (country == null)
                    throw new InvalidOperationException($"Country not found: {countryCode}");

                // Load existing locations and companies for caching
                var existingLocations = await context.Locations
                    .Where(l => l.CountryId == country.Id)
                    .ToListAsync(cancellationToken);
                var locationCache = existingLocations
                    .GroupBy(l => l.LocationName?.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var existingCompanies = await context.Companies
                    .Where(c => c.CountryId == country.Id)
                    .ToListAsync(cancellationToken);
                var companyCache = existingCompanies
                    .GroupBy(c => c.CompanyName?.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                int page = 1, totalPages = 1, totalFetched = 0, savedCount = 0, consecutiveNoInsert = 0;

                do
                {
                    _logger.LogDebug("Fetching page {Page} for {CountryCode}", page, countryCode);

                    var apiUrl = $"http://public.api.careerjet.net/search?affid={_apiKey}&category=it&locale_code={locale}&pagesize={ResultsPerPage}&page={page}&user_ip={_userIp}&user_agent={Uri.EscapeDataString(_userAgent)}";
                    var json = await _httpClient.GetStringAsync(apiUrl, cancellationToken);

                    if (page == 1)
                    {
                        _logger.LogDebug("First Careerjet API response sample for {CountryCode}: {JsonSample}",
                            countryCode, json.Length > 500 ? json[..500] + "..." : json);
                    }

                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (page == 1 && root.TryGetProperty("pages", out var pagesProp))
                    {
                        totalPages = pagesProp.GetInt32();
                        _logger.LogInformation("Total pages available for {CountryCode}: {TotalPages}", countryCode, totalPages);
                    }

                    if (!root.TryGetProperty("jobs", out var jobsProp) || jobsProp.ValueKind != JsonValueKind.Array)
                    {
                        _logger.LogWarning("Invalid jobs data in Careerjet API response for {CountryCode}", countryCode);
                        break;
                    }

                    // Map to JobPost objects
                    var jobs = await CareerjetJobMapper.MapToJobPostsAsync(
                        jobsProp, country, locationCache, companyCache, context);

                    totalFetched += jobs.Count;

                    if (jobs.Count == 0)
                    {
                        _logger.LogInformation("No recent jobs (within 1 month) found on page {Page} for {CountryCode}",
                            page, countryCode);
                        break;
                    }

                    // ALWAYS resolve ALL URLs to ensure we get actual URLs
                    await ResolveAllUrlsAsync(jobs, countryCode, cancellationToken);

                    // Check for existing jobs using composite hash
                    var compositeHashes = jobs.Select(j => j.CompositeHash).Where(h => !string.IsNullOrEmpty(h)).ToArray();
                    var existingHashes = await context.JobPosts
                        .Where(j => j.DataSource == "Careerjet" && j.CountryId == country.Id && compositeHashes.Contains(j.CompositeHash))
                        .Select(j => j.CompositeHash)
                        .ToListAsync(cancellationToken);

                    var toInsert = jobs.Where(j => !existingHashes.Contains(j.CompositeHash)).ToList();

                    _logger.LogDebug("Page {Page} for {CountryCode} - fetched: {FetchedCount}, duplicates: {DuplicateCount}, new: {NewCount}",
                        page, countryCode, jobs.Count, existingHashes.Count, toInsert.Count);

                    if (toInsert.Count == 0)
                    {
                        consecutiveNoInsert++;
                        if (consecutiveNoInsert >= 3)
                        {
                            _logger.LogInformation("No new jobs inserted for 3 consecutive pages. Stopping fetch early for {CountryCode}", countryCode);
                            break;
                        }
                    }
                    else
                    {
                        consecutiveNoInsert = 0;
                        // Additional duplicate check within the batch
                        var uniqueJobs = toInsert
                            .GroupBy(j => j.CompositeHash)
                            .Select(g => g.First())
                            .ToList();

                        context.JobPosts.AddRange(uniqueJobs);
                        await context.SaveChangesAsync(cancellationToken);
                        savedCount += uniqueJobs.Count;
                    }

                    page++;
                    if (page <= totalPages)
                        await Task.Delay(DelayMs, cancellationToken);

                } while (page <= totalPages);

                var result = new FetchResultsDTO
                {
                    TotalJobs = totalFetched,
                    TotalPages = totalPages,
                    SavedJobs = savedCount
                };

                _logger.LogInformation("Fetch completed for {CountryCode}: Pages={TotalPages}, TotalJobs={TotalFetched}, Saved={SavedCount}",
                    countryCode, totalPages, totalFetched, savedCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Careerjet fetch operation for {CountryCode}", countryCode);
                throw;
            }
        }

        private async Task ResolveAllUrlsAsync(List<JobPost> jobs, string countryCode, CancellationToken cancellationToken)
        {
            if (jobs == null || jobs.Count == 0) return;

            var jobsToResolve = jobs.Where(j => !string.IsNullOrWhiteSpace(j.Url)).ToList();

            if (jobsToResolve.Count == 0) return;

            _logger.LogDebug("Resolving {Count} URLs for {CountryCode} to ensure actual URLs are saved",
                jobsToResolve.Count, countryCode);

            var sem = new SemaphoreSlim(UrlResolveConcurrency);
            var resolvedCount = 0;
            var trackingUrlCount = 0;

            var tasks = jobsToResolve.Select(async job =>
            {
                await sem.WaitAsync(cancellationToken);
                try
                {
                    var originalUrl = job.Url;
                    var isTracking = IsTrackingUrl(originalUrl);

                    if (isTracking)
                    {
                        Interlocked.Increment(ref trackingUrlCount);
                    }

                    var actualUrl = await ResolveFinalUrlAsync(originalUrl, cancellationToken);

                    if (!string.IsNullOrWhiteSpace(actualUrl))
                    {
                        // Always update if we got a different URL, or if original was a tracking URL
                        if (!actualUrl.Equals(originalUrl, StringComparison.OrdinalIgnoreCase))
                        {
                            job.Url = actualUrl;
                            Interlocked.Increment(ref resolvedCount);

                            if (isTracking)
                            {
                                _logger.LogTrace("Resolved tracking URL: {Original} -> {Resolved}", originalUrl, actualUrl);
                            }
                            else
                            {
                                _logger.LogTrace("URL redirected: {Original} -> {Resolved}", originalUrl, actualUrl);
                            }
                        }
                    }
                    else if (isTracking)
                    {
                        _logger.LogWarning("Failed to resolve tracking URL, keeping original (will expire): {Url}", originalUrl);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error resolving URL for job: {Url}", job.Url);
                }
                finally
                {
                    sem.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("URL Resolution for {CountryCode}: {ResolvedCount} URLs resolved, {TrackingCount} tracking URLs found",
                countryCode, resolvedCount, trackingUrlCount);
        }

        private static bool IsTrackingUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            var lowerUrl = url.ToLowerInvariant();
            return lowerUrl.Contains("jobviewtrack.com") ||
                   lowerUrl.Contains("redirect") ||
                   lowerUrl.Contains("track") ||
                   lowerUrl.Contains("affiliate") ||
                   lowerUrl.Contains("goto") ||
                   lowerUrl.Contains("click") ||
                   lowerUrl.Contains("ref=");
        }

        private async Task<string?> ResolveFinalUrlAsync(string url, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            try
            {
                var currentUrl = url;
                var redirectCount = 0;
                var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using var handler = new HttpClientHandler()
                {
                    AllowAutoRedirect = false,
                    UseCookies = false
                };

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(UrlResolveTimeoutSeconds);

                // Add more realistic headers
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
                client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");

                while (redirectCount < MaxRedirects)
                {
                    // Prevent infinite loops
                    if (visitedUrls.Contains(currentUrl))
                    {
                        _logger.LogDebug("Circular redirect detected for URL: {Url}", currentUrl);
                        break;
                    }
                    visitedUrls.Add(currentUrl);

                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Head, currentUrl);
                        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                        // Handle redirect status codes
                        if (IsRedirectStatusCode(response.StatusCode))
                        {
                            var location = response.Headers.Location?.ToString();

                            if (string.IsNullOrWhiteSpace(location))
                            {
                                _logger.LogDebug("Redirect response without Location header for: {Url}", currentUrl);
                                break;
                            }

                            // Handle relative URLs
                            if (location.StartsWith("/"))
                            {
                                var uri = new Uri(currentUrl);
                                location = $"{uri.Scheme}://{uri.Host}{location}";
                            }
                            else if (!location.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                var uri = new Uri(currentUrl);
                                location = $"{uri.Scheme}://{uri.Host}/{location.TrimStart('/')}";
                            }

                            currentUrl = location;
                            redirectCount++;

                            _logger.LogTrace("Following redirect #{RedirectCount}: {Url}", redirectCount, location);
                        }
                        else if (response.IsSuccessStatusCode)
                        {
                            // Final destination reached
                            break;
                        }
                        else
                        {
                            _logger.LogDebug("HTTP {StatusCode} when resolving URL: {Url}", response.StatusCode, currentUrl);

                            // For client errors, return the current URL as we can't resolve further
                            if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                            {
                                break;
                            }

                            // For server errors, we might want to try the original URL
                            return url;
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogDebug(ex, "HTTP request failed for URL: {Url}", currentUrl);
                        break;
                    }
                    catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                    {
                        _logger.LogDebug("Timeout resolving URL: {Url}", currentUrl);
                        break;
                    }
                }

                if (redirectCount >= MaxRedirects)
                {
                    _logger.LogWarning("Maximum redirects ({MaxRedirects}) exceeded for URL: {Url}", MaxRedirects, url);
                }

                return currentUrl;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to resolve URL: {Url}", url);
                return null;
            }
        }

        private static bool IsRedirectStatusCode(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.MovedPermanently ||     // 301
                   statusCode == HttpStatusCode.Found ||                // 302
                   statusCode == HttpStatusCode.SeeOther ||             // 303
                   statusCode == HttpStatusCode.TemporaryRedirect ||    // 307
                   statusCode == HttpStatusCode.PermanentRedirect;      // 308
        }

        private static string MapCountryCodeToLocale(string countryCode) => countryCode.Trim().ToUpperInvariant() switch
        {
            "NO" => "no_NO",
            "DK" => "da_DK",
            _ => throw new ArgumentOutOfRangeException(nameof(countryCode), $"Unsupported country code: {countryCode}")
        };
    }
}
