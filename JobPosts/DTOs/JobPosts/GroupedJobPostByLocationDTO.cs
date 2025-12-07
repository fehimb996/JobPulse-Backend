namespace JobPosts.DTOs.JobPosts
{
    public class GroupedJobPostByLocationDTO
    {
        public string LocationName { get; set; } = default!;
        public int LocationId{ get; set; } = default!;
        public List<PostsByLocationDTO> Posts { get; set; } = new();
    }

}
