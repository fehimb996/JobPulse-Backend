using JobPosts.Data;
using JobPosts.DTOs;
using JobPosts.DTOs.JobPosts;
using JobPosts.Queries.FavoriteJobs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.FavoriteJobs
{
    public class GetFavoriteJobsQueryHandler : IRequestHandler<GetFavoriteJobsQuery, List<JobPostDTO>>
    {
        private readonly JobPostsDbContext _context;

        public GetFavoriteJobsQueryHandler(JobPostsDbContext context) => _context = context;

        public async Task<List<JobPostDTO>> Handle(GetFavoriteJobsQuery request, CancellationToken cancellationToken)
        {
            // Query the join table and project directly to DTOs; avoids loading User entity or full graph.
            var favorites = await _context.UserFavoriteJobs
                .AsNoTracking()
                .Where(ufj => ufj.UserId == request.UserId && ufj.JobPost != null)
                .Select(ufj => ufj.JobPost!) // navigation to JobPost
                .OrderByDescending(j => j.Created)
                .Select(j => new JobPostDTO
                {
                    Id = j.Id,
                    Title = j.Title,
                    Description = j.Description,
                    Url = j.Url,
                    SalaryMin = j.SalaryMin == null ? null : (double?)j.SalaryMin, // adapt types if needed
                    SalaryMax = j.SalaryMax == null ? null : (double?)j.SalaryMax,
                    Created = j.Created,

                    CountryName = j.Country != null ? j.Country.CountryName : null,
                    CompanyName = j.Company != null ? j.Company.CompanyName : null,
                    LocationName = j.Location != null ? j.Location.LocationName : null,
                    ContractType = j.ContractType != null ? j.ContractType.Type : null,
                    ContractTime = j.ContractTime != null ? j.ContractTime.Time : null,
                    WorkLocation = j.WorkplaceModel != null ? j.WorkplaceModel.Workplace : null,

                    Skills = j.JobPostSkills != null
                        ? j.JobPostSkills.Select(js => js.Skill.SkillName).ToList()
                        : new List<string>(),

                    Languages = j.JobPostLanguages != null
                        ? j.JobPostLanguages.Select(jl => jl.Language.Name).ToList()
                        : new List<string>()
                })
                .ToListAsync(cancellationToken);

            return favorites;
        }
    }
}
