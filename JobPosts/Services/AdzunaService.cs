using JobPosts.DTOs;
using JobPosts.Interfaces;
using JobPosts.Models;
using JobPosts.Options;
using JobPosts.Parsers;
using JobPosts.Providers;
using JobPosts.Repositories;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;

namespace JobPosts.Services
{
    public class AdzunaService
    {
        private readonly HttpClient _httpClient;
        private readonly AdzunaRepository _repository;
        private readonly IAdzunaCredentialProvider _credentialProvider;

        public AdzunaService(HttpClient httpClient, AdzunaRepository repository, IAdzunaCredentialProvider credentialProvider)
        {
            _httpClient = httpClient;
            _repository = repository;
            _credentialProvider = credentialProvider;
        }

        public async Task<(int TotalJobs, int TotalPages, int SavedCount)> FetchAndSaveJobsAsync(string countryCode = "be")
        {
            const int resultsPerPage = 50;
            int page = 1;
            int maxRetries = 3;
            int retryDelay = 5;
            int? totalPages = null;
            int totalJobs = 0;
            int savedCount = 0;
            int consecutiveNoInsertPages = 0;

            Console.WriteLine($"\n\t\t\t------- Starting job fetch for country: [{countryCode.ToUpper()}] -------\n");

            while (true)
            {
                var credential = _credentialProvider.GetNextCredential();
                if (credential == null)
                {
                    Console.WriteLine("\n\t\t\t------- All credentials exhausted or blocked. Stopping. -------\n");
                    break;
                }

                var appId = credential.AppId;
                var appKey = credential.AppKey;

                Console.WriteLine($"\n\t\t\t------- Fetching page [{page}] using AppId [{appId}]...\n");

                var builder = new UriBuilder($"https://api.adzuna.com/v1/api/jobs/{countryCode}/search/{page}");
                var queryParams = new Dictionary<string, string>
                {
                    ["app_id"] = appId,
                    ["app_key"] = appKey,
                    ["category"] = "it-jobs",
                    ["results_per_page"] = resultsPerPage.ToString(),
                    ["sort_by"] = "date"
                };
                builder.Query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

                HttpResponseMessage? response = null;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
                        response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                        if (response.StatusCode == HttpStatusCode.BadGateway && attempt < maxRetries)
                        {
                            Console.WriteLine($"\n\t\t\t------- 502 on attempt {attempt}, retrying...\n");
                            await Task.Delay(retryDelay * 1000);
                            continue;
                        }

                        response.EnsureSuccessStatusCode();
                        break;
                    }
                    catch (Exception ex) when (attempt < maxRetries)
                    {
                        Console.WriteLine($"\n\t\t\t------- Error on attempt {attempt}: {ex.Message}. Retrying...\n");
                        await Task.Delay(retryDelay * 1000);
                    }
                }

                if (response == null)
                {
                    _credentialProvider.MarkCredentialAsExhausted(credential);
                    Console.WriteLine("\n\t\t\t------- All retry attempts failed. Marking credential as exhausted. -------\n");
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var jsonText = Encoding.UTF8.GetString(bytes);

                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;

                if (totalPages == null)
                {
                    totalJobs = root.GetProperty("count").GetInt32();
                    totalPages = (totalJobs + resultsPerPage - 1) / resultsPerPage;
                    Console.WriteLine($"\n\t\t-------> Total jobs available: [{totalJobs}], across [{totalPages}] pages <-------\n");
                }

                var jobsThisPage = AdzunaParser.ParseFromJson(doc, countryCode);

                int inserted = 0;
                if (jobsThisPage.Any())
                {
                    inserted = await _repository.AddRangeAsync(jobsThisPage);
                    savedCount += inserted;

                    Console.WriteLine($"\n\t\t-------> Country [{countryCode.ToUpper()}], Page [{page}] — inserted [{inserted}] new jobs <-------\n");
                }
                else
                {
                    Console.WriteLine($"\n\t\t\t------- Page [{page}] — no jobs to process -------\n");
                }

                if (inserted == 0)
                {
                    consecutiveNoInsertPages++;
                    if (consecutiveNoInsertPages >= 3)
                    {
                        Console.WriteLine("\n\t\t------- No new jobs inserted for 3 consecutive pages. Stopping fetch to avoid exhausting credentials. -------\n");
                        break;
                    }
                }
                else
                {
                    consecutiveNoInsertPages = 0;
                }

                if (page >= totalPages)
                    break;

                page++;
                await Task.Delay(500);
            }

            Console.WriteLine($"\n\t\t------- Fetch complete: pages = [{totalPages}], totalJobs = [{totalJobs}], saved = [{savedCount}] -------");
            return (totalJobs, totalPages ?? 0, savedCount);
        }

        public async Task<List<AdzunaTopCompanyJobCountDTO>> GetTopCompaniesByJobCountAsync(string? country = null)
        {
            return await _repository.GetTopCompaniesByJobCountAsync(country);
        }

        public async Task<List<AdzunaCountrySalaryRangeDTO>> GetAverageSalaryByCountryAsync(string? country = null)
        {
            return await _repository.GetAverageSalaryByCountryAsync(country);
        }

        public async Task<List<AdzunaTopJobTitleDTO>> GetTopJobTitlesAsync()
        {
            return await _repository.GetTopJobTitlesAsync();
        }

        public async Task<List<AdzunaCompanySalaryRangeDTO>> GetSalaryRangeByCompanyAsync(string? country = null)
        {
            return await _repository.GetSalaryRangeByCompanyAsync(country);
        }

        public async Task<AdzunaRecentJobPostResponse> GetRecentPostsPerCompanyAsync(
            string country,
            string company,
            int page,
            int pageSize,
            string? contractType = null,
            string? contractTime = null)
        {
            return await _repository.GetRecentPostsPerCompanyAsync(country, company, page, pageSize, contractType, contractTime);
        }

        public async Task<AdzunaRecentJobPostsExtendedDTO> GetRecentPostsPerCountryAsync(
            string country,
            int page,
            int pageSize,
            int timeframeInWeeks,
            string? contractType = null,
            string? contractTime = null,
            string? workLocation = null,
            string? title = null,
            string? location = null, 
            string? company = null)
        {
            return await _repository.GetRecentPostsPerCountryAsync(country, page, pageSize, timeframeInWeeks, contractType, contractTime, workLocation, title, location, company);
        }

        public async Task<List<AdzunaCountryCountDTO>> GetCountryCountsAsync(int timeframeInWeeks)
        {
            return await _repository.GetCountryCountsAsync(timeframeInWeeks);
        }

        public async Task<List<AdzunaContractTimeCountDTO>> GetJobCountsByContractTimeAsync(string country, int timeframeInWeeks, string? contractType, string? workLocation, string? location, string? company, string? title)
        {
            return await _repository.GetJobCountsByContractTimeAsync(country, timeframeInWeeks, contractType, workLocation, location, company, title);
        }

        public async Task<List<AdzunaContractTypeCountDTO>> GetJobCountsByContractTypeAsync(string country, int timeframeInWeeks, string? contractTime, string? workLocation, string? location, string? company, string? title)
        {
            return await _repository.GetJobCountsByContractTypeAsync(country, timeframeInWeeks, contractTime, workLocation, location, company, title);
        }

        public async Task<List<AdzunaWorkLocationCountDTO>> GetJobCountsByWorkLocationAsync(string country, int timeframeInWeeks, string? contractType, string? contractTime, string? location, string? company, string? title)
        {
            return await _repository.GetJobCountsByWorkLocationAsync(country, timeframeInWeeks, contractType, contractTime, location, company, title);
        }

        public async Task<List<AdzunaCompanyJobCountDTO>> GetCompanyJobCountsAsync(string country, int timeframeInWeeks, string? contractType, string? contractTime, string? workLocation, string? location, string? title)
        {
            return await _repository.GetCompanyJobCountsAsync(country, timeframeInWeeks, contractType, contractTime, workLocation, location, title);
        }

        public async Task<List<AdzunaLocationJobCountDTO>> GetLocationJobCountsAsync(string country, int timeframeInWeeks, string? contractType, string? contractTime, string? workLocation, string? company, string? title)
        {
            return await _repository.GetLocationJobCountsAsync(country, timeframeInWeeks, contractType, contractTime, workLocation, company, title);
        }

        public async Task<List<AdzunaRecentJobPostDTO>> SearchByTitleAndLocationAsync(
            string? keyword,
            string country,
            string? location,
            int page,
            int pageSize)
        {
            return await _repository.SearchByTitleAndLocationAsync(keyword, country, location, page, pageSize);
        }

        public async Task<List<string>> GetTitleSuggestionsAsync(string input, string country, int timeframeInWeeks)
        {
            return await _repository.GetTitleSuggestionsAsync(input, country, timeframeInWeeks);
        }

        public async Task<List<string>> GetLocationSuggestionsAsync(string input, string country, int timeframeInWeeks)
        {
            return await _repository.GetLocationSuggestionsAsync(input, country, timeframeInWeeks);
        }

        public async Task<List<string>> GetCompanySuggestionsAsync(string input, string company, int timeframeInWeeks)
        {
            return await _repository.GetCompanySuggestionsAsync(input, company, timeframeInWeeks);
        }

        public async Task<AdzunaJobDTO?> GetJobDetailsAsync(long jobId)
        {
            var job = await _repository.GetJobByIdAsync(jobId);
            if (job == null) return null;

            return new AdzunaJobDTO
            {
                JobId = job.JobId,
                Title = job.Title,
                Company = job.Company,
                Location = job.Location,
                Description = job.Description,
                FullDescription = job.FullDescription,
                Url = job.Url,
                ContractType = job.ContractType,
                ContractTime = job.ContractTime,
                SalaryMin = job.SalaryMin,
                SalaryMax = job.SalaryMax,
                Created = job.Created,
                Country = job.Country,
                Summary = job.Summary,
                WorkLocation = job.WorkLocation
            };
        }
    }
}