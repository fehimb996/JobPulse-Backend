using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobPosts.Models
{
    public class Location
    {
        public int Id { get; set; }

        [MaxLength(120)]
        public string? LocationName { get; set; }

        [MaxLength(255)]
        public string? Area { get; set; }

        [Column(TypeName = "decimal(9, 6)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(9, 6)")]
        public decimal? Longitude { get; set; }

        public int CountryId { get; set; }
        public Country Country { get; set; }

        public ICollection<JobPost> JobPosts { get; set; } = new List<JobPost>();
    }
}
