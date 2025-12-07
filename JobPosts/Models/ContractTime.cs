using System.ComponentModel.DataAnnotations;

namespace JobPosts.Models
{
    public class ContractTime
    {
        public int Id { get; set; }

        [MaxLength(20)]
        public string Time { get; set; }

        public ICollection<JobPost> JobPosts { get; set; } = new List<JobPost>();
    }
}
