using JobPosts.DTOs;
using Microsoft.Identity.Client;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobPosts.Models
{
    public class JobPost
    {
        public int Id { get; set; }
        public long? JobId { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? FullDescription { get; set; }

        private string _url = string.Empty;

        [MaxLength(800)]
        public string Url
        {
            get => _url;
            set
            {
                _url = value ?? string.Empty;
                IsDetailsUrl = _url.Contains("details", StringComparison.OrdinalIgnoreCase);

            }
        }

        public double? SalaryMin { get; set; }
        public double? SalaryMax { get; set; }

        [MaxLength(50)]
        public string? Salary { get; set; }

        [Column(TypeName = "decimal(9, 6)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(9, 6)")]
        public decimal? Longitude { get; set; }

        public DateTime Created { get; set; }
        public DateTime? ProcessDate { get; set; } = DateTime.UtcNow;

        public int? Scrape { get; set; }
        public int? IsInactive { get; set; }

        public bool IsDetailsUrl { get; set; }

        [MaxLength(20)]
        public string? DataSource { get; set; } // e.g., "Careerjet", "Adzuna"

        public int CountryId { get; set; }
        public Country Country { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int? LocationId { get; set; }
        public Location? Location { get; set; }

        public int? ContractTypeId { get; set; }
        public ContractType? ContractType { get; set; }

        public int? ContractTimeId { get; set; }
        public ContractTime? ContractTime { get; set; }

        public int? WorkplaceModelId { get; set; }
        public WorkplaceModel? WorkplaceModel { get; set; }

        public ICollection<JobPostSkill> JobPostSkills { get; set; } = new List<JobPostSkill>();
        public ICollection<JobPostLanguage> JobPostLanguages { get; set; } = new List<JobPostLanguage>();
        public ICollection<UserFavoriteJob> UsersWhoFavorited { get; set; } = new List<UserFavoriteJob>();

        private string _title = string.Empty;

        [MaxLength(255)]
        public string Title
        {
            get => _title;
            set
            {
                _title = value?.Trim() ?? string.Empty;
                TitleNormalized = NormalizeTitle(_title);
            }
        }

        [MaxLength(255)]
        public string? TitleNormalized { get; set; }

        public static string NormalizeTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            var s = title.Trim().ToLowerInvariant();

            // Remove punctuation and special characters but keep diacritics
            s = s
                .Replace("-", "")
                .Replace("_", "")
                .Replace(",", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("/", "")
                .Replace("\\", "")
                .Replace(":", "")
                .Replace(";", "")
                .Replace("\"", "")
                .Replace("'", "")
                .Trim();

            // Remove non-letter, non-digit characters but preserve Unicode letters (including diacritics) and IT keywords
            s = System.Text.RegularExpressions.Regex.Replace(s, @"[^\p{L}\p{N}\.\+#\s]", "");

            // Replace multiple spaces with single space, then remove all spaces
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Replace(" ", "");

            // Clean up multiple dots
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\.{2,}", ".");
            s = s.TrimEnd('.');

            return s.Length > 255 ? s[..255] : s;
        }

        public JobPost()
        {
            _title = string.Empty;
            _url = string.Empty;
            TitleNormalized = string.Empty;
            IsDetailsUrl = false;
        }

        public static bool CheckIfDetailsUrl(string? url)
        {
            return !string.IsNullOrEmpty(url) &&
                   url.Contains("details", StringComparison.OrdinalIgnoreCase);
        }

        public void UpdateIsDetailsUrl()
        {
            IsDetailsUrl = CheckIfDetailsUrl(Url);
        }

        [MaxLength(64)]
        public string? CompositeHash { get; set; }

        // Method to compute composite hash for deduplication
        public static string ComputeCompositeHash(string? title, string? companyName, string? locationName, string? countryCode, DateTime? created = null)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            // Normalize inputs for consistent hashing
            var normalizedTitle = NormalizeTitle(title);
            var normalizedCompany = NormalizeCompanyName(companyName);
            var normalizedLocation = NormalizeLocationName(locationName);
            var normalizedCountry = countryCode?.ToUpperInvariant() ?? string.Empty;

            // Include date if provided (within same week to handle slight timing differences)
            var dateComponent = created?.ToString("yyyy-MM-dd") ?? string.Empty;

            var composite = $"{normalizedTitle}|{normalizedCompany}|{normalizedLocation}|{normalizedCountry}|{dateComponent}";

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(composite));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private static string NormalizeCompanyName(string? companyName)
        {
            if (string.IsNullOrWhiteSpace(companyName))
                return string.Empty;

            var normalized = companyName.Trim().ToLowerInvariant();

            // Remove common company suffixes and legal entities
            var suffixes = new[] { "as", "asa", "ab", "ltd", "limited", "inc", "corporation", "corp", "llc", "gmbh", "sarl" };
            foreach (var suffix in suffixes)
            {
                if (normalized.EndsWith($" {suffix}"))
                {
                    normalized = normalized.Substring(0, normalized.Length - suffix.Length - 1).Trim();
                    break;
                }
            }

            // Remove special characters but keep basic punctuation
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\p{L}\p{N}\s\-&]", "");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");

            return normalized.Trim();
        }

        private static string NormalizeLocationName(string? locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName))
                return string.Empty;

            var normalized = locationName.Trim().ToLowerInvariant();

            // Handle common location formats
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s*,\s*", ",");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\p{L}\p{N}\s,\-]", "");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");

            return normalized.Trim();
        }
    }
}
