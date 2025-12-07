using JobPosts.Data;
using JobPosts.DTOs.JobPosts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Queries.JobPosts
{
    public class GetPostsByLocationIdQueryHandler : IRequestHandler<GetPostsByLocationIdQuery, PostsByLocationPagedResultDTO>
    {
        private readonly JobPostsDbContext _context;
        public GetPostsByLocationIdQueryHandler(JobPostsDbContext context)
        {
            _context = context;
        }

        public async Task<PostsByLocationPagedResultDTO> Handle(GetPostsByLocationIdQuery request, CancellationToken cancellationToken)
        {
            var fromDate = DateTime.UtcNow.AddDays(-7 * request.TimeframeInWeeks);

            var query = _context.JobPosts
                .AsNoTracking()
                .Include(j => j.Location)
                .Include(j => j.ContractType)
                .Include(j => j.ContractTime)
                .Include(j => j.WorkplaceModel)
                .Include(j => j.JobPostSkills).ThenInclude(js => js.Skill)
                .Include(j => j.JobPostLanguages).ThenInclude(jl => jl.Language)
                .Where(j =>j.Created >= fromDate && j.LocationId == request.LocationId);

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

            var posts = await query
                .OrderByDescending(j => j.Created)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(j => new JobPostsByLocationDTO
                {
                    Id = j.Id,
                    Title = j.Title,
                    Description = j.Description,
                    Url = j.Url,
                    SalaryMin = j.SalaryMin,
                    SalaryMax = j.SalaryMax,
                    Created = j.Created,
                    LocationName = j.Location != null ? j.Location.LocationName : null,
                    ContractType = j.ContractType != null ? j.ContractType.Type : null,
                    ContractTime = j.ContractTime != null ? j.ContractTime.Time : null,
                    WorkLocation = j.WorkplaceModel != null ? j.WorkplaceModel.Workplace : null,
                    Skills = j.JobPostSkills.Select(js => js.Skill.SkillName).ToList(),
                    Languages = j.JobPostLanguages.Select(jl => jl.Language.Name).ToList()
                })
                .ToListAsync(cancellationToken);

            return new PostsByLocationPagedResultDTO
            {
                Posts = posts,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
            };
        }
    }
}
