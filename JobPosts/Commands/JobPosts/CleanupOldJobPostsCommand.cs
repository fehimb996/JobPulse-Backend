using MediatR;
using JobPosts.DTOs.JobPosts;

namespace JobPosts.Commands.JobPosts
{
    public record CleanupOldJobPostsCommand : IRequest<CleanupResultDTO>;
}
