using System.ComponentModel.DataAnnotations;

namespace JobPosts.Models
{
    public class ContractType
    {
        public int Id { get; set; }

        [MaxLength(20)]
        public string Type { get; set; }

        public ICollection<JobPost> JobPosts { get; set; } = new List<JobPost>();
    }
}
