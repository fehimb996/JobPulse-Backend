using System.ComponentModel.DataAnnotations;

namespace JobPosts.Models
{
    public class Country
    {
        public int Id { get; set; }

        [MaxLength(40)]
        public string CountryName { get; set; }

        [MaxLength(3)]
        public string CountryCode { get; set; }

        public ICollection<JobPost> JobPosts { get; set; } = new List<JobPost>();
        public ICollection<Location> Locations { get; set; } = new List<Location>();
        public ICollection<Company> Companies { get; set; } = new List<Company>();
    }
}
