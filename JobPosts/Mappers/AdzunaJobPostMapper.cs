using JobPosts.Data;
using JobPosts.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace JobPosts.Mappers
{
    public static class AdzunaJobPostMapper
    {
        public static async Task<List<JobPost>> MapToJobPostsAsync(
            JsonDocument doc,
            string countryCode,
            JobPostsDbContext context)
        {
            var jobs = new List<JobPost>();
            var resultsArr = doc.RootElement.GetProperty("results").EnumerateArray();

            var country = await context.Countries
                .FirstOrDefaultAsync(c => c.CountryCode.ToLower() == countryCode.ToLower());

            var contractTypes = await context.ContractTypes
                .AsNoTracking()
                .ToDictionaryAsync(ct => ct.Type.ToLower());

            var contractTimes = await context.ContractTimes
                .AsNoTracking()
                .ToDictionaryAsync(ct => ct.Time.ToLower());

            var existingLocations = await context.Locations
                .Where(l => l.CountryId == country.Id)
                .ToListAsync();

            var locationCache = existingLocations
                .GroupBy(l => l.LocationName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var existingCompanies = await context.Companies
                .Where(c => c.CountryId == country.Id)
                .ToListAsync();

            var companyCache = existingCompanies
                .GroupBy(c => c.CompanyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var item in resultsArr)
            {
                if (!item.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
                    continue;
                if (!long.TryParse(idProp.GetString(), out var jobId))
                    continue;

                var job = new JobPost
                {
                    JobId = jobId,
                    Title = item.GetProperty("title").GetString(),
                    Description = item.TryGetProperty("description", out var d) ? d.GetString() : null,
                    FullDescription = null,
                    Url = item.GetProperty("redirect_url").GetString(),
                    Created = DateTime.TryParse(item.GetProperty("created").GetString(), out var cd)
                                      ? cd : DateTime.UtcNow,
                    ProcessDate = DateTime.UtcNow,
                    SalaryMin = item.TryGetProperty("salary_min", out var sMin) && sMin.ValueKind == JsonValueKind.Number
                                      ? sMin.GetDouble() : null,
                    SalaryMax = item.TryGetProperty("salary_max", out var sMax) && sMax.ValueKind == JsonValueKind.Number
                                      ? sMax.GetDouble() : null,
                    Country = country,
                    DataSource = "Adzuna",

                    Latitude = item.TryGetProperty("latitude", out var latProp) && latProp.ValueKind == JsonValueKind.Number
                      ? Math.Round((decimal)latProp.GetDouble(), 6)
                      : null,
                    Longitude = item.TryGetProperty("longitude", out var lonProp) && lonProp.ValueKind == JsonValueKind.Number
                      ? Math.Round((decimal)lonProp.GetDouble(), 6)
                      : null
                };

                var ctKey = item.TryGetProperty("contract_type", out var ctProp)
                    ? ctProp.GetString()?.ToLower() : null;

                if (ctKey != null && contractTypes.TryGetValue(ctKey, out var ct))
                    job.ContractTypeId = ct.Id;


                var tmKey = item.TryGetProperty("contract_time", out var tmProp)
                    ? tmProp.GetString()?.ToLower() : null;

                string NormalizeContractTime(string key) => key switch
                {
                    "full_time" => "Full time",
                    "part_time" => "Part time",
                    _ => null
                };

                var normalizedTime = tmKey != null ? NormalizeContractTime(tmKey) : null;

                if (normalizedTime != null && contractTimes.TryGetValue(normalizedTime.ToLower(), out var tm))
                    job.ContractTimeId = tm.Id;


                var areaName = item.TryGetProperty("location", out var locProp) &&
               locProp.TryGetProperty("area", out var areaArr) &&
               areaArr.ValueKind == JsonValueKind.Array &&
               areaArr.GetArrayLength() > 1
    ? string.Join(", ", areaArr.EnumerateArray().Select(a => a.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)))
    : null;

                var locationName = item.TryGetProperty("location", out var locProp2) &&
                                   locProp2.TryGetProperty("display_name", out var locName)
                    ? locName.GetString()?.Trim()
                    : null;

                if (!string.IsNullOrWhiteSpace(locationName))
                {
                    if (!locationCache.TryGetValue(locationName, out var locEntity))
                    {
                        locEntity = new Location
                        {
                            LocationName = locationName,
                            Area = areaName,
                            Country = country
                        };
                        context.Locations.Add(locEntity);
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
                }

                var companyName = item.TryGetProperty("company", out var compProp)
                                  && compProp.TryGetProperty("display_name", out var compNameEl)
                    ? compNameEl.GetString()?.Trim()
                    : null;

                if (!string.IsNullOrWhiteSpace(companyName))
                {
                    if (!companyCache.TryGetValue(companyName, out var compEntity))
                    {
                        var lowerCompanyName = companyName.ToLower();

                        compEntity = await context.Companies
                            .Where(c => c.CountryId == country.Id &&
                                        c.CompanyName.ToLower() == lowerCompanyName)
                            .FirstOrDefaultAsync();

                        if (compEntity == null)
                        {
                            compEntity = new Company
                            {
                                CompanyName = companyName,
                                Country = country
                            };
                            context.Companies.Add(compEntity);
                        }

                        companyCache[companyName] = compEntity;
                    }

                    job.Company = compEntity;
                }
            jobs.Add(job);
            }
            return jobs;
        }
    }
}
