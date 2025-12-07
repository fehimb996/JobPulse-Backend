using System.ComponentModel.DataAnnotations;

namespace JobPosts.Models
{
    public class Company
    {
        public int Id { get; set; }

        [MaxLength(150)]
        public string? CompanyName { get; set; }

        [MaxLength(255)]
        public string? Url { get; set; }

        public int CountryId { get; set; }
        public Country Country { get; set; }

        public ICollection<JobPost> JobPosts { get; set; } = new List<JobPost>();
    }
}
