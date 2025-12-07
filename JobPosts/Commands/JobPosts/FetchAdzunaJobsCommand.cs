using JobPosts.DTOs.JobPosts;
using MediatR;

namespace JobPosts.Commands.JobPosts
{
    public record FetchAdzunaJobsCommand(string CountryCode) : IRequest<FetchResultsDTO>;
}
