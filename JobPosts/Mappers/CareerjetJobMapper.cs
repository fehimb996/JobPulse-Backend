using JobPosts.Data;
using JobPosts.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Mappers
{
    public static class CareerjetJobMapper
    {
        public static async Task<List<JobPost>> MapToJobPostsAsync(
            JsonElement jobsElement,
            Country country,
            Dictionary<string, Location> locationCache,
            Dictionary<string, Company> companyCache,
            JobPostsDbContext context)
        {
            var jobs = new List<JobPost>();
            var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);

            foreach (var jobEl in jobsElement.EnumerateArray())
            {
                // require url
                if (!jobEl.TryGetProperty("url", out var urlProp))
                    continue;
                var jobUrl = urlProp.GetString();
                if (string.IsNullOrWhiteSpace(jobUrl))
                    continue;

                var title = jobEl.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                var description = jobEl.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                var salary = jobEl.TryGetProperty("salary", out var salaryProp) ? salaryProp.GetString() : null;

                // Parse created date and filter out jobs older than 1 month
                DateTime? created = null;
                if (jobEl.TryGetProperty("date", out var dateProp))
                {
                    if (DateTime.TryParse(dateProp.GetString(), out var createdDate))
                    {
                        created = createdDate;
                        if (created < oneMonthAgo)
                            continue;
                    }
                }

                // Extract names for composite hash used for deduplication
                var companyName = ExtractCompanyName(jobEl);
                var locationName = ExtractLocationName(jobEl);
                var compositeHash = JobPost.ComputeCompositeHash(title, companyName, locationName, country.CountryCode, created);

                if (string.IsNullOrEmpty(compositeHash))
                    continue; // skip if essential data missing

                var job = new JobPost
                {
                    // JobId removed - let it remain null/default
                    Url = jobUrl,
                    Title = title ?? string.Empty,
                    Description = description,
                    Salary = salary,
                    Created = created ?? DateTime.UtcNow,
                    ProcessDate = DateTime.UtcNow,
                    Country = country,
                    CountryId = country.Id,
                    CompositeHash = compositeHash,
                    DataSource = "Careerjet"
                };

                // Salary parsing
                if (!string.IsNullOrWhiteSpace(job.Salary))
                {
                    var (minSalary, maxSalary) = ParseSalary(job.Salary);
                    job.SalaryMin = minSalary;
                    job.SalaryMax = maxSalary;
                }

                // Location & company processing
                await ProcessLocationAsync(jobEl, job, country, locationCache, context);
                await ProcessCompanyAsync(jobEl, job, country, companyCache, context);

                jobs.Add(job);
            }

            return jobs;
        }

        #region Location & Company helpers (full implementations)

        private static async Task ProcessLocationAsync(
            JsonElement jobEl,
            JobPost job,
            Country country,
            Dictionary<string, Location> locationCache,
            JobPostsDbContext context)
        {
            string? locationName = null;
            string? areaName = null;

            if (jobEl.TryGetProperty("location", out var locProp))
            {
                if (locProp.ValueKind == JsonValueKind.String)
                {
                    locationName = locProp.GetString()?.Trim();
                }
                else if (locProp.ValueKind == JsonValueKind.Object)
                {
                    locationName = locProp.TryGetProperty("display_name", out var locNameEl)
                        ? locNameEl.GetString()?.Trim()
                        : null;

                    areaName = locProp.TryGetProperty("area", out var areaArr) &&
                               areaArr.ValueKind == JsonValueKind.Array
                        ? string.Join(", ", areaArr.EnumerateArray()
                            .Select(a => a.GetString())
                            .Where(s => !string.IsNullOrWhiteSpace(s)))
                        : null;
                }
            }
            else if (jobEl.TryGetProperty("locations", out var locationsProp))
            {
                if (locationsProp.ValueKind == JsonValueKind.String)
                {
                    locationName = locationsProp.GetString()?.Trim();
                }
                else if (locationsProp.ValueKind == JsonValueKind.Array)
                {
                    locationName = string.Join(", ", locationsProp.EnumerateArray()
                        .Select(l => l.GetString())
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                }
            }

            if (string.IsNullOrWhiteSpace(locationName)) return;

            if (!locationCache.TryGetValue(locationName, out var locEntity))
            {
                locEntity = await context.Locations
                    .Where(l => l.CountryId == country.Id && l.LocationName.ToLower() == locationName.ToLower())
                    .FirstOrDefaultAsync();

                if (locEntity == null)
                {
                    locEntity = new Location
                    {
                        LocationName = locationName,
                        Area = areaName,
                        Country = country,
                        CountryId = country.Id
                    };
                    context.Locations.Add(locEntity);
                }

                locationCache[locationName] = locEntity;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(locEntity.Area) && !string.IsNullOrWhiteSpace(areaName))
                {
                    locEntity.Area = areaName;
                }
            }

            job.Location = locEntity;
            if (locEntity.Id != 0) job.LocationId = locEntity.Id;
        }

        private static async Task ProcessCompanyAsync(
            JsonElement jobEl,
            JobPost job,
            Country country,
            Dictionary<string, Company> companyCache,
            JobPostsDbContext context)
        {
            string? companyName = null;
            string? companyUrl = null;

            if (jobEl.TryGetProperty("company", out var compProp))
            {
                if (compProp.ValueKind == JsonValueKind.String)
                {
                    companyName = compProp.GetString()?.Trim();
                }
                else if (compProp.ValueKind == JsonValueKind.Object)
                {
                    companyName = compProp.TryGetProperty("display_name", out var compNameEl)
                        ? compNameEl.GetString()?.Trim()
                        : null;

                    companyUrl = compProp.TryGetProperty("url", out var compUrlEl)
                        ? compUrlEl.GetString()?.Trim()
                        : null;
                }
            }

            if (string.IsNullOrWhiteSpace(companyName)) return;

            if (!companyCache.TryGetValue(companyName, out var compEntity))
            {
                compEntity = await context.Companies
                    .Where(c => c.CountryId == country.Id && c.CompanyName.ToLower() == companyName.ToLower())
                    .FirstOrDefaultAsync();

                if (compEntity == null)
                {
                    compEntity = new Company
                    {
                        CompanyName = companyName,
                        Url = companyUrl,
                        Country = country,
                        CountryId = country.Id
                    };
                    context.Companies.Add(compEntity);
                }

                companyCache[companyName] = compEntity;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(compEntity.Url) && !string.IsNullOrWhiteSpace(companyUrl))
                {
                    compEntity.Url = companyUrl;
                }
            }

            job.Company = compEntity;
            if (compEntity.Id != 0) job.CompanyId = compEntity.Id;
        }

        #endregion

        private static string? ExtractCompanyName(JsonElement jobEl)
        {
            if (jobEl.TryGetProperty("company", out var compProp))
            {
                if (compProp.ValueKind == JsonValueKind.String)
                    return compProp.GetString()?.Trim();
                if (compProp.ValueKind == JsonValueKind.Object &&
                    compProp.TryGetProperty("display_name", out var nameEl))
                    return nameEl.GetString()?.Trim();
            }
            return null;
        }

        private static string? ExtractLocationName(JsonElement jobEl)
        {
            if (jobEl.TryGetProperty("location", out var locProp))
            {
                if (locProp.ValueKind == JsonValueKind.String)
                    return locProp.GetString()?.Trim();
                if (locProp.ValueKind == JsonValueKind.Object &&
                    locProp.TryGetProperty("display_name", out var nameEl))
                    return nameEl.GetString()?.Trim();
            }

            if (jobEl.TryGetProperty("locations", out var locationsProp))
            {
                if (locationsProp.ValueKind == JsonValueKind.String)
                    return locationsProp.GetString()?.Trim();
                if (locationsProp.ValueKind == JsonValueKind.Array)
                {
                    return string.Join(", ", locationsProp.EnumerateArray()
                        .Select(l => l.GetString())
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                }
            }
            return null;
        }

        private static (double? min, double? max) ParseSalary(string salary)
        {
            if (string.IsNullOrWhiteSpace(salary)) return (null, null);

            var numbers = System.Text.RegularExpressions.Regex.Matches(salary, @"\d+(?:[.,]\d+)*")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => double.TryParse(m.Value.Replace(",", ""), out var val) ? val : (double?)null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            return numbers.Count switch
            {
                0 => (null, null),
                1 => (numbers[0], numbers[0]),
                _ => (numbers.Min(), numbers.Max())
            };
        }
    }
}
