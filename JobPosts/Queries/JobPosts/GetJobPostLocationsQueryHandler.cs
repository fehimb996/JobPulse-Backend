using JobPosts.Data;
using JobPosts.DTOs.JobPosts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Queries.JobPosts
{
    public class GetJobPostLocationsQueryHandler : IRequestHandler<GetPostLocationsQuery, JobPostGroupedPagedResultDTO>
    {
        private readonly JobPostsDbContext _context;
        public GetJobPostLocationsQueryHandler(JobPostsDbContext context)
        {
            _context = context;
        }

        public async Task<JobPostGroupedPagedResultDTO> Handle(GetPostLocationsQuery request, CancellationToken cancellationToken)
        {
            var fromDate = DateTime.UtcNow.AddDays(-7 * request.TimeframeInWeeks);

            var query = _context.JobPosts
                .AsSplitQuery()
                .AsNoTrackingWithIdentityResolution()              
                .Where(j => j.Country.CountryCode == request.CountryCode && j.Created >= fromDate);

            if (!string.IsNullOrWhiteSpace(request.ContractType))
                query = query.Where(j => j.ContractType != null && j.ContractType.Type == request.ContractType);
            if (!string.IsNullOrWhiteSpace(request.ContractTime))
                query = query.Where(j => j.ContractTime != null && j.ContractTime.Time == request.ContractTime);
            if (!string.IsNullOrWhiteSpace(request.WorkLocation))
                query = query.Where(j => j.WorkplaceModel != null && j.WorkplaceModel.Workplace == request.WorkLocation);
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                var titleInput = request.Title;
                query = query.Where(j =>
                    j.Title != null &&
                    EF.Functions.Collate(j.Title, "Latin1_General_CI_AI").Contains(titleInput));
            }

            if (!string.IsNullOrWhiteSpace(request.Company))
            {
                var companyInput = request.Company;
                query = query.Where(j =>
                    j.Company != null &&
                    EF.Functions.Collate(j.Company.CompanyName!, "Latin1_General_CI_AI").Contains(companyInput));
            }

            if (!string.IsNullOrWhiteSpace(request.Location))
            {
                var locationInput = request.Location;
                query = query.Where(j =>
                    j.Location != null &&
                    EF.Functions.Collate(j.Location.LocationName!, "Latin1_General_CI_AI").Contains(locationInput));
            }

            if (request.Skills != null && request.Skills.Any())
            {
                foreach (var skill in request.Skills)
                {
                    var skillLower = skill.ToLower();
                    query = query.Where(j => j.JobPostSkills.Any(js => js.Skill.SkillName.ToLower() == skillLower));
                }
            }

            if (request.Languages != null && request.Languages.Any())
            {
                var requestedLanguagesLower = request.Languages.Select(l => l.ToLower()).ToList();

                query = query.Where(j => j.JobPostLanguages.Any(jl => requestedLanguagesLower.Contains(jl.Language.Name.ToLower())));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var postsGrouped = await query
                .OrderByDescending(j => j.Created)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(j => new
                {
                    LocationName = j.Location != null ? j.Location.LocationName : "Unknown",
                    JobPost = new PostsByLocationDTO
                    {
                        Id = j.Id,
                        Title = j.Title,
                        Description = j.Description,
                        Url = j.Url,
                        SalaryMin = j.SalaryMin,
                        SalaryMax = j.SalaryMax,
                        Created = j.Created,
                        LocationId = j.LocationId ?? 0,
                        CountryName = j.Country.CountryName,
                        CompanyName = j.Company != null ? j.Company.CompanyName : null,
                        LocationName = j.Location != null ? j.Location.LocationName : null,
                        ContractType = j.ContractType != null ? j.ContractType.Type : null,
                        ContractTime = j.ContractTime != null ? j.ContractTime.Time : null,
                        WorkLocation = j.WorkplaceModel != null ? j.WorkplaceModel.Workplace : null,
                        Skills = j.JobPostSkills.Select(js => js.Skill.SkillName).ToList(),
                        Languages = j.JobPostLanguages.Select(jl => jl.Language.Name).ToList()
                    }
                })
                .GroupBy(x => x.LocationName)
                .Select(g => new GroupedJobPostByLocationDTO
                {
                    LocationId = g.First().JobPost.LocationId,
                    LocationName = g.Key ?? "",
                    Posts = g.Select(x => x.JobPost).ToList()
                })
                .ToListAsync(cancellationToken);

            return new JobPostGroupedPagedResultDTO
            {
                GroupedPosts = postsGrouped,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
            };
        }
    }
}
