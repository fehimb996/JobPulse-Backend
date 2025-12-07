using JobPosts.Models;
using System.Text.Json;

namespace JobPosts.Parsers
{
    public class AdzunaParser
    {
        public static List<Adzuna> ParseFromJson(JsonDocument doc, string countryCode)
        {
            var jobs = new List<Adzuna>();
            var resultsArr = doc.RootElement.GetProperty("results").EnumerateArray();

            string? SafeGetString(JsonElement el, string propName) =>
                el.TryGetProperty(propName, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetString() : null;

            string? SafeGetNested(JsonElement el, string parent, string child) =>
                el.TryGetProperty(parent, out var obj) && obj.ValueKind == JsonValueKind.Object
                    ? SafeGetString(obj, child)
                    : null;

            foreach (var item in resultsArr)
            {
                if (!item.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
                    continue;

                if (!long.TryParse(idProp.GetString(), out var jobId))
                    continue;

                var job = new Adzuna
                {
                    JobId = jobId,
                    Title = SafeGetString(item, "title"),
                    Company = SafeGetNested(item, "company", "display_name"),
                    Location = SafeGetNested(item, "location", "display_name"),
                    Description = SafeGetString(item, "description"),
                    Url = SafeGetString(item, "redirect_url"),
                    Category = SafeGetNested(item, "category", "label"),
                    ContractType = SafeGetString(item, "contract_type"),
                    ContractTime = SafeGetString(item, "contract_time"),
                    Country = countryCode.ToUpper(),
                    ProcessDate = DateTime.Now
                };

                if (item.TryGetProperty("created", out var c) &&
                    c.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(c.GetString(), out var created) &&
                    created >= new DateTime(1753, 1, 1))
                {
                    job.Created = created;
                }

                if (item.TryGetProperty("salary_min", out var sMin) && sMin.ValueKind == JsonValueKind.Number)
                    job.SalaryMin = sMin.GetDouble();

                if (item.TryGetProperty("salary_max", out var sMax) && sMax.ValueKind == JsonValueKind.Number)
                    job.SalaryMax = sMax.GetDouble();

                jobs.Add(job);
            }

            return jobs;
        }
    }
}
