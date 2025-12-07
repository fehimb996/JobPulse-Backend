using Hangfire.Common;
using JobPosts.Data;
using JobPosts.DTOs;
using JobPosts.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace JobPosts.Repositories
{
    public class AdzunaRepository
    {
        private readonly JobPostsDbContext _context;

        public static readonly string[] Countries = { "US", "DE", "GB", "NL", "BE", "AT", "CH" };

        public AdzunaRepository(JobPostsDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistsAsync(long jobId)
        {
            return await _context.AdzunaJobs.AnyAsync(j => j.JobId == jobId);
        }

        public async Task AddAsync(Adzuna job)
        {
            _context.AdzunaJobs.Add(job);
            await _context.SaveChangesAsync();
        }

        public async Task<int> AddRangeAsync(List<Adzuna> jobs)
        {
            var jobIds = jobs.Select(j => j.JobId).ToList();

            var existingIds = await _context.AdzunaJobs
                .Where(j => jobIds.Contains(j.JobId))
                .Select(j => j.JobId)
                .ToListAsync();

            var newJobs = jobs.Where(j => !existingIds.Contains(j.JobId)).ToList();

            Console.WriteLine($"------- Received: {jobs.Count}, Skipped (duplicate): {existingIds.Count}, Inserted: {newJobs.Count} -------");

            if (existingIds.Any())
            {
                Console.WriteLine("------- Skipped Job IDs: " + string.Join(", ", existingIds));
            }

            if (newJobs.Any())
            {
                await _context.AddRangeAsync(newJobs);
                await _context.SaveChangesAsync();

                foreach (var entry in _context.ChangeTracker.Entries())
                {
                    entry.State = EntityState.Detached;
                }
            }

            return newJobs.Count;
        }

        public async Task<List<long>> GetExistingJobIdsAsync(List<long> ids)
        {
            return await _context.AdzunaJobs
                .Where(j => ids.Contains(j.JobId))
                .Select(j => j.JobId)
                .ToListAsync();
        }

        public async Task<List<AdzunaTopCompanyJobCountDTO>> GetTopCompaniesByJobCountAsync(string? country = null)
        {
            var oneWeekAgo = DateTime.UtcNow.AddDays(-7);

            var query = _context.AdzunaJobs
                .Where(j => !string.IsNullOrEmpty(j.Company) && j.Created >= oneWeekAgo);

            if (!string.IsNullOrWhiteSpace(country))
                query = query.Where(j => j.Country == country);
            else
                query = query.Where(j => Countries.Contains(j.Country!));

            var groupedResults = await query
                .GroupBy(j => new { j.Country, j.Company })
                .Select(g => new AdzunaTopCompanyJobCountDTO
                {
                    Country = g.Key.Country,
                    Company = g.Key.Company,
                    JobCount = g.Count()
                })
                .ToListAsync();

            return groupedResults
                .GroupBy(x => x.Country)
                .SelectMany(g => g.OrderByDescending(x => x.JobCount).Take(10))
                .OrderBy(x => x.Country)
                .ThenByDescending(x => x.JobCount)
                .ToList();
        }

        public async Task<List<AdzunaCountrySalaryRangeDTO>> GetAverageSalaryByCountryAsync(string? country = null)
        {
            var recentDate = DateTime.UtcNow.AddMonths(-1);

            var query = _context.AdzunaJobs
                .Where(j =>
                    j.SalaryMin != null &&
                    j.SalaryMax != null &&
                    j.Created >= recentDate);

            if (!string.IsNullOrWhiteSpace(country))
            {
                query = query.Where(j => j.Country == country);
            }
            else
            {
                query = query.Where(j => Countries.Contains(j.Country!));
            }

            return await query
                .GroupBy(j => j.Country)
                .Select(g => new AdzunaCountrySalaryRangeDTO
                {
                    Country = g.Key,
                    AvgMinSalary = g.Average(j => j.SalaryMin!.Value),
                    AvgMaxSalary = g.Average(j => j.SalaryMax!.Value)
                })
                .OrderBy(r => r.Country)
                .ToListAsync();
        }

        public async Task<List<AdzunaTopJobTitleDTO>> GetTopJobTitlesAsync()
        {
            return await _context.AdzunaJobs
                .Where(j =>
                    Countries.Contains(j.Country!) &&
                    j.Created >= DateTime.UtcNow.AddDays(-7) &&
                    j.Title != null && j.Title != "")
                .GroupBy(j => j.Title)
                .Select(g => new AdzunaTopJobTitleDTO
                {
                    Title = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(30)
                .ToListAsync();
        }

        public async Task<List<AdzunaCompanySalaryRangeDTO>> GetSalaryRangeByCompanyAsync(string? country = null)
        {
            var cutoffDate = DateTime.UtcNow.AddMonths(-1);

            var baseQuery = _context.AdzunaJobs
                .Where(j =>
                    j.Company != null &&
                    j.SalaryMin != null &&
                    j.SalaryMax != null &&
                    j.Created >= cutoffDate);

            if (!string.IsNullOrWhiteSpace(country))
            {
                baseQuery = baseQuery.Where(j => j.Country == country);
            }
            else
            {
                baseQuery = baseQuery.Where(j => Countries.Contains(j.Country!));
            }

            var groupedData = await baseQuery
                .GroupBy(j => new { j.Company, j.Country })
                .Select(g => new AdzunaCompanySalaryRangeDTO
                {
                    Company = g.Key.Company!,
                    Country = g.Key.Country!,
                    AvgSalaryMin = (decimal)g.Average(j => j.SalaryMin!.Value),
                    AvgSalaryMax = (decimal)g.Average(j => j.SalaryMax!.Value),
                    JobCount = g.Count()
                })
                .ToListAsync();

            var topCompanies = groupedData
                .GroupBy(x => x.Country)
                .SelectMany(g => g
                    .OrderByDescending(x => x.AvgSalaryMax)
                    .ThenByDescending(x => x.JobCount)
                    .ThenBy(x => x.Company)
                    .Take(10))
                .OrderBy(x => x.Country)
                .ThenByDescending(x => x.AvgSalaryMax)
                .ToList();

            return topCompanies;
        }

        public async Task<AdzunaRecentJobPostsExtendedDTO> GetRecentPostsPerCountryAsync(
            string country,
            int page,
            int pageSize,
            int timeframeInWeeks = 1,
            string? contractType = null,
            string? contractTime = null,
            string? workLocation = null,
            string? title = null,
            string? location = null,
            string? company = null)
        {
            var fromDate = DateTime.UtcNow.AddDays(-7 * timeframeInWeeks);
            var query = _context.AdzunaJobs
                .AsNoTracking()
                .Where(j => j.Country == country && j.Created >= fromDate);

            if (!string.IsNullOrWhiteSpace(contractType))
                query = query.Where(j => j.ContractType == contractType);

            if (!string.IsNullOrWhiteSpace(contractTime))
                query = query.Where(j => j.ContractTime == contractTime);

            if (!string.IsNullOrWhiteSpace(workLocation))
                query = query.Where(j => j.WorkLocation != null && j.WorkLocation.Trim() == workLocation);

            if (!string.IsNullOrWhiteSpace(title))
            {
                var titleLower = title.ToLower();
                query = query.Where(j => j.Title != null && j.Title.ToLower().Contains(titleLower));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                var locationLower = location.ToLower();
                query = query.Where(j => j.Location != null && j.Location.ToLower().Contains(locationLower));
            }

            if (!string.IsNullOrWhiteSpace(company))
            {
                var companyLower = company.ToLower();
                query = query.Where(j => j.Company != null && j.Company.ToLower().Contains(companyLower));
            }

            var totalCount = await query.CountAsync();

            var posts = await query
                .OrderByDescending(j => j.Created)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new AdzunaRecentJobPostDTO
                {
                    JobId = j.JobId,
                    Country = j.Country,
                    Company = j.Company,
                    Title = j.Title,
                    Location = j.Location,
                    Description = j.Description,
                    SalaryMin = j.SalaryMin,
                    SalaryMax = j.SalaryMax,
                    Created = j.Created,
                    Url = j.Url,
                    ContractType = j.ContractType,
                    ContractTime = j.ContractTime,
                    WorkLocation = j.WorkLocation
                })
                .ToListAsync();

            var contractTypeCounts = await query
                .Where(j => j.ContractType != null)
                .GroupBy(j => j.ContractType)
                .Select(g => new AdzunaContractTypeCountDTO
                {
                    ContractType = g.Key!,
                    JobCount = g.Count()
                })
                .OrderByDescending(x => x.JobCount)
                .ToListAsync();

            var contractTimeCounts = await query
                .Where(j => j.ContractTime != null)
                .GroupBy(j => j.ContractTime)
                .Select(g => new AdzunaContractTimeCountDTO
                {
                    ContractTime = g.Key!,
                    JobCount = g.Count()
                })
                .OrderByDescending(x => x.JobCount)
                .ToListAsync();

            var workLocationCounts = await query
                .Where(j => j.WorkLocation != null)
                .GroupBy(j => j.WorkLocation)
                .Select(g => new AdzunaWorkLocationCountDTO
                {
                    WorkLocation = g.Key!,
                    JobCount = g.Count()
                })
                .OrderByDescending(x => x.JobCount)
                .ToListAsync();

            var companyCounts = await query
                .Where(j => j.Company != null)
                .GroupBy(j => j.Company)
                .Select(g => new AdzunaCompanyJobCountDTO
                {
                    Company = g.Key!,
                    JobCount = g.Count()
                })
                .OrderByDescending(x => x.JobCount)
                .ToListAsync();

            var locationCounts = await query
                .Where(j => j.Location != null)
                .GroupBy(j => j.Location)
                .Select(g => new AdzunaLocationJobCountDTO
                {
                    Location = g.Key!,
                    JobCount = g.Count()
                })
                .OrderByDescending(x => x.JobCount)
                .ToListAsync();

            return new AdzunaRecentJobPostsExtendedDTO
            {
                Posts = posts,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                ContractTypeCounts = contractTypeCounts,
                ContractTimeCounts = contractTimeCounts,
                WorkLocationCounts = workLocationCounts,
                CompanyCounts = companyCounts,
                LocationCounts = locationCounts
            };
        }

        public async Task<List<AdzunaCountryCountDTO>> GetCountryCountsAsync(int timeframeInWeeks)
        {
            var sinceDate = DateTime.UtcNow.AddDays(-7 * timeframeInWeeks);

            return await _context.AdzunaJobs
                .AsNoTracking()
                .Where(j => j.Created >= sinceDate && !string.IsNullOrEmpty(j.Country))
                .GroupBy(j => j.Country!)
                .Select(g => new AdzunaCountryCountDTO
                {
                    Country = g.Key,
                    JobCount = g.Count()
                })
                .OrderByDescending(x => x.JobCount)
                .ToListAsync();
        }

        public async Task<List<AdzunaContractTimeCountDTO>> GetJobCountsByContractTimeAsync(
            string country,
            int timeframeInWeeks = 1,
            string? contractType = null,
            string? workLocation = null,
            string? company = null,
            string? location = null,
            string? title = null)
        {
            var sinceDate = DateTime.UtcNow.AddDays(-7 * timeframeInWeeks);

            var query = _context.AdzunaJobs.AsNoTracking()
                .Where(j => j.Country == country &&
                            j.ContractTime != null &&
                            j.Created >= sinceDate);

            if (!string.IsNullOrEmpty(contractType))
                query = query.Where(j => j.ContractType == contractType);

            if (!string.IsNullOrEmpty(workLocation))
                query = query.Where(j => j.WorkLocation == workLocation);

            if (!string.IsNullOrEmpty(company))
                query = query.Where(j => j.Company == company);

            if (!string.IsNullOrEmpty(location))
                query = query.Where(j => j.Location == location);

            if (!string.IsNullOrWhiteSpace(title))
            {
                var titleLower = title.ToLower();
                query = query.Where(j => j.Title != null && EF.Functions.Like(j.Title.ToLower(), $"%{titleLower}%"));
            }

            return await query
                .GroupBy(j => j.ContractTime)
                .Select(g => new AdzunaContractTimeCountDTO
                {
                    ContractTime = g.Key!,
                    JobCount = g.Count()
                })
                .OrderByDescending(x => x.JobCount)
                .ToListAsync();
        }

        public async Task<List<AdzunaContractTypeCountDTO>> GetJobCountsByContractTypeAsync(
            string country,
            int timeframeInWeeks = 1,
            string? contractTime = null,
            string? workLocation = null,
            string? company = null,
            string? location = null,
            string? title = null)
        {
            var sinceDate = DateTime.UtcNow.AddDays(-7 * timeframeInWeeks);

            var query = _context.AdzunaJobs.AsNoTracking()
                .Where(j => j.Country == country &&
                            j.ContractType != null &&
                            j.Created >= sinceDate);

            if (!string.IsNullOrEmpty(contractTime))
                query = query.Where(j => j.ContractTime == contractTime);

            if (!string.IsNullOrEmpty(workLocation))
                query = query.Where(j => j.WorkLocation == workLocation);

            if (!string.IsNullOrEmpty(company))
                query = query.Where(j => j.Company == company);

            if (!string.IsNullOrEmpty(location))
                query = query.Where(j => j.Location == location);

            if (!string.IsNullOrWhiteSpace(title))
            {
                var titleLower = title.ToLower();
                query = query.Where(j => j.Title != null && EF.Functions.Like(j.Title.ToLower(), $"%{titleLower}%"));
            }

            return await query
                .GroupBy(j => j.ContractType)
                .Select(g => new AdzunaContractTypeCountDTO
                {
                    ContractType = g.Key!,
                    JobCount = g.Count()
                })
                .OrderByDescending(x => x.JobCount)
                .ToListAsync();
        }

        public async Task<List<AdzunaWorkLocationCountDTO>> GetJobCountsByWorkLocationAsync(
            string country,
            int timeframeInWeeks = 1,
            string? contractType = null,
            string? contractTime = null,
            string? company = null,
            string? location = null,
            string? title = null)
        {
            var sinceDate = DateTime.UtcNow.AddDays(-7 * timeframeInWeeks);

            var query = _context.AdzunaJobs.AsNoTracking()
                .Where(j => j.Country == country &&
                            j.WorkLocation != null &&
                            j.Created >= sinceDate);

            if (!string.IsNullOrEmpty(contractType))
                query = query.Where(j => j.ContractType == contractType);

            if (!string.IsNullOrEmpty(contractTime))
                query = query.Where(j => j.ContractTime == contractTime);

            if (!string.IsNullOrEmpty(company))
                query = query.Where(j => j.Company == company);

            if (!string.IsNullOrEmpty(location))
                query = query.Where(j => j.Location == location);

            if (!string.IsNullOrWhiteSpace(title))
            {
                var titleLower = title.ToLower();
                query = query.Where(j => j.Title != null && EF.Functions.Like(j.Title.ToLower(), $"%{titleLower}%"));
            }

            return await query
                .GroupBy(j => j.WorkLocation)
                .Select(g => new AdzunaWorkLocationCountDTO
                {
                    WorkLocation = g.Key!,
                    JobCount = g.Count()
                })
                .OrderByDescending(x => x.JobCount)
                .ToListAsync();
        }

        public async Task<List<AdzunaCompanyJobCountDTO>> GetCompanyJobCountsAsync(
             string country,
             int timeframeInWeeks = 1,
             string? contractType = null,
             string? contractTime = null,
             string? workLocation = null,
             string? location = null,
             string? title = null)
        {
            var sinceDate = DateTime.UtcNow.AddDays(-7 * timeframeInWeeks);

            var query = _context.AdzunaJobs.AsNoTracking()
                .Where(j => j.Country == country &&
                            j.Company != null &&
                            j.Created >= sinceDate);

            if (!string.IsNullOrEmpty(contractType))
                query = query.Where(j => j.ContractType == contractType);

            if (!string.IsNullOrEmpty(contractTime))
                query = query.Where(j => j.ContractTime == contractTime);

            if (!string.IsNullOrEmpty(workLocation))
                query = query.Where(j => j.WorkLocation == workLocation);

            if (!string.IsNullOrEmpty(location))
                query = query.Where(j => j.Location == location);

            if (!string.IsNullOrWhiteSpace(title))
            {
                var titleLower = title.ToLower();
                query = query.Where(j => j.Title != null && EF.Functions.Like(j.Title.ToLower(), $"%{titleLower}%"));
            }

            return await query
                .GroupBy(j => j.Company)
                .Select(g => new AdzunaCompanyJobCountDTO
                {
                    Company = g.Key!,
                    JobCount = g.Count()
                })
                .OrderByDescending(x => x.JobCount)
                .ToListAsync();
        }

        public async Task<List<AdzunaLocationJobCountDTO>> GetLocationJobCountsAsync(
            string country,
            int timeframeInWeeks = 1,
            string? contractType = null,
            string? contractTime = null,
            string? workLocation = null,
            string? company = null,
            string? title = null)
        {
            var sinceDate = DateTime.UtcNow.AddDays(-7 * timeframeInWeeks);

            var query = _context.AdzunaJobs.AsNoTracking()
                .Where(j => j.Country == country &&
                            j.Location != null &&
                            j.Created >= sinceDate);

            if (!string.IsNullOrEmpty(contractType))
                query = query.Where(j => j.ContractType == contractType);

            if (!string.IsNullOrEmpty(contractTime))
                query = query.Where(j => j.ContractTime == contractTime);

            if (!string.IsNullOrEmpty(workLocation))
                query = query.Where(j => j.WorkLocation == workLocation);

            if (!string.IsNullOrEmpty(company))
                query = query.Where(j => j.Company == company);

            if (!string.IsNullOrWhiteSpace(title))
            {
                var titleLower = title.ToLower();
                query = query.Where(j => j.Title != null && EF.Functions.Like(j.Title.ToLower(), $"%{titleLower}%"));
            }

            return await query
                .GroupBy(j => j.Location)
                .Select(g => new AdzunaLocationJobCountDTO
                {
                    Location = g.Key!,
                    JobCount = g.Count()
                })
                .OrderByDescending(x => x.JobCount)
                .ToListAsync();
        }

        public async Task<List<AdzunaRecentJobPostDTO>> SearchByTitleAndLocationAsync(string? keyword, string country, string? location, int page, int pageSize)
        {
            var query = _context.AdzunaJobs
                .Where(j => j.Country == country);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.ToLower().Replace("-", "").Replace(" ", "");
                query = query.Where(j =>
                    j.Title != null &&
                    j.Title.ToLower().Replace("-", "").Replace(" ", "").Contains(normalizedKeyword));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                var normalizedLocation = location.ToLower();
                query = query.Where(j =>
                    j.Location != null &&
                    j.Location.ToLower().Contains(normalizedLocation));
            }

            return await query
                .OrderByDescending(j => j.Created)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new AdzunaRecentJobPostDTO
                {
                    Country = j.Country,
                    Company = j.Company,
                    Title = j.Title,
                    Location = j.Location,
                    SalaryMin = j.SalaryMin,
                    SalaryMax = j.SalaryMax,
                    Created = j.Created,
                    Url = j.Url
                })
                .ToListAsync();
        }

        public async Task<List<string>> GetTitleSuggestionsAsync(string input, string country, int timeframeInWeeks = 1)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(country))
                return new List<string>();

            var normalizedInput = input.ToLower();
            var sinceDate = DateTime.UtcNow.AddDays(-7 * timeframeInWeeks);

            return await _context.AdzunaJobs
                .Where(j => j.Country == country &&
                            j.Created >= sinceDate &&
                            j.Title != null &&
                            j.Title.ToLower().Contains(normalizedInput))
                .GroupBy(j => j.Title.Trim())
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(10)
                .ToListAsync();
        }

        public async Task<List<string>> GetLocationSuggestionsAsync(string input, string country, int timeframeInWeeks = 1)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(country))
                return new List<string>();

            var normalizedInput = input.ToLower();
            var sinceDate = DateTime.UtcNow.AddDays(-7 * timeframeInWeeks);

            return await _context.AdzunaJobs
                .Where(j => j.Country == country &&
                            j.Created >= sinceDate &&
                            j.Location != null)
                .Select(j => j.Location.Trim())
                .Where(loc => loc.ToLower().Contains(normalizedInput))
                .Distinct()
                .OrderBy(loc => loc)
                .Take(10)
                .ToListAsync();
        }

        public async Task<List<string>> GetCompanySuggestionsAsync(string input, string country, int timeframeInWeeks = 1)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(country))
                return new List<string>();

            var normalizedInput = input.ToLower();
            var sinceDate = DateTime.UtcNow.AddDays(-7 * timeframeInWeeks);

            return await _context.AdzunaJobs
                .Where(j => j.Country == country &&
                            j.Created >= sinceDate &&
                            j.Company != null)
                .Select(j => j.Company.Trim())
                .Where(comp => comp.ToLower().Contains(normalizedInput))
                .Distinct()
                .OrderBy(comp => comp)
                .Take(10)
                .ToListAsync();
        }

        public async Task<AdzunaRecentJobPostResponse> GetRecentPostsPerCompanyAsync(
            string country,
            string company,
            int page,
            int pageSize,
            string? contractType = null,
            string? contractTime = null)
        {
            var oneWeekAgo = DateTime.UtcNow.AddDays(-7);

            var query = _context.AdzunaJobs
                .Where(j =>
                    j.Country == country &&
                    j.Company != null &&
                    EF.Functions.Like(j.Company.ToLower(), $"%{company}%") &&
                    j.Created != null &&
                    j.Created >= oneWeekAgo);

            if (!string.IsNullOrWhiteSpace(contractType))
                query = query.Where(j => j.ContractType == contractType);

            if (!string.IsNullOrWhiteSpace(contractTime))
                query = query.Where(j => j.ContractTime == contractTime);

            var posts = await query
                .OrderByDescending(j => j.Created)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new AdzunaRecentJobPostDTO
                {
                    JobId = j.JobId,
                    Country = j.Country,
                    Company = j.Company,
                    Title = j.Title,
                    Location = j.Location,
                    Description = j.Description,
                    SalaryMin = j.SalaryMin,
                    SalaryMax = j.SalaryMax,
                    Created = j.Created,
                    Url = j.Url
                })
                .ToListAsync();

            var count = await query.CountAsync();

            return new AdzunaRecentJobPostResponse
            {
                Jobs = posts,
                TotalCount = count,
                TotalPages = (int)Math.Ceiling((double)count / pageSize)
            };
        }

        public async Task<Adzuna?> GetJobByIdAsync(long jobId)
        {
            return await _context.AdzunaJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        }
    }
}
