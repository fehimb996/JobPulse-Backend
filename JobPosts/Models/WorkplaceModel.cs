using System.ComponentModel.DataAnnotations;

namespace JobPosts.Models
{
    public class WorkplaceModel
    {
        public int Id { get; set; }

        [MaxLength(20)]
        public string Workplace { get; set; }

        public ICollection<JobPost> JobPosts { get; set; } = new List<JobPost>();
    }
}
