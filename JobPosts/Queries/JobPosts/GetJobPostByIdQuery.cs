using JobPosts.DTOs.JobPosts;
using MediatR;

namespace JobPosts.Queries.JobPosts
{
    public class GetJobPostByIdQuery : IRequest<JobPostDetailsDTO?>
    {
        public int Id { get; set; }

        public GetJobPostByIdQuery(int id)
        {
            Id = id;
        }
    }
}
