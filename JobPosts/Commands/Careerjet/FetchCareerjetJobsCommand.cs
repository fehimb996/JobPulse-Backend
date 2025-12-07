using JobPosts.DTOs.JobPosts;
using MediatR;

namespace JobPosts.Commands.Careerjet
{
    public record FetchCareerjetJobsCommand(string CountryCode) : IRequest<FetchResultsDTO>;
}
