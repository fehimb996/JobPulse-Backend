using System.ComponentModel.DataAnnotations;

namespace JobPosts.Models
{
    public class Skill
    {
        public int Id { get; set; }

        [MaxLength(50)]
        public string SkillName { get; set; }

        public ICollection<JobPostSkill> JobPostSkills { get; set; } = new List<JobPostSkill>();
    }
}
