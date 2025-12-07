namespace JobPosts.Models
{
    public class UserFavoriteJob
    {
        public string UserId { get; set; } = null!;
        public int JobPostId { get; set; }

        public ApplicationUser User { get; set; } = null!;
        public JobPost JobPost { get; set; } = null!;
    }
}
