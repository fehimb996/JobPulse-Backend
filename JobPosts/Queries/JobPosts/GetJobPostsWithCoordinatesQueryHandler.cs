using JobPosts.Data;
using JobPosts.DTOs.JobPosts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Queries.JobPosts
{
    public class GetJobPostsWithCoordinatesQueryHandler : IRequestHandler<GetJobPostsWithCoordinatesQuery, JobPostsWithCoordinatesResultDTO>
    {
        private readonly JobPostsDbContext _context;

        public GetJobPostsWithCoordinatesQueryHandler(JobPostsDbContext context)
        {
            _context = context;
        }

        public async Task<JobPostsWithCoordinatesResultDTO> Handle(GetJobPostsWithCoordinatesQuery request, CancellationToken cancellationToken)
        {
            var fromDate = DateTime.UtcNow.AddDays(-7 * request.TimeframeInWeeks);

            var query = _context.JobPosts
                .AsSplitQuery()
                .AsNoTrackingWithIdentityResolution()
                .Include(j => j.Location)
                .Include(j => j.Country)
                .Include(j => j.Company)
                .Include(j => j.ContractType)
                .Include(j => j.ContractTime)
                .Include(j => j.WorkplaceModel)
                .Include(j => j.JobPostSkills).ThenInclude(js => js.Skill)
                .Include(j => j.JobPostLanguages).ThenInclude(jl => jl.Language)
                .Where(j => j.Country.CountryCode == request.CountryCode && j.Created >= fromDate);

            // Apply filters
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

            if (request.LocationId.HasValue)
            {
                query = query.Where(j => j.LocationId == request.LocationId.Value);
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

            // Determine page size and skip logic
            var effectivePageSize = request.GetAll ? int.MaxValue : request.PageSize;
            var skip = request.GetAll ? 0 : (request.Page - 1) * request.PageSize;
            var take = request.GetAll ? totalCount : request.PageSize;

            List<JobPostWithCoordinatesDTO> jobPosts;

            if (request.SummaryMode)
            {
                // In summary mode, we only need location info and counts, not job details
                jobPosts = new List<JobPostWithCoordinatesDTO>();
            }
            else
            {
                jobPosts = await query
                    .OrderByDescending(j => j.Created)
                    .Skip(skip)
                    .Take(take)
                    .Select(j => new JobPostWithCoordinatesDTO
                    {
                        Id = j.Id,
                        Title = j.Title,
                        Description = j.Description,
                        Url = j.Url,
                        SalaryMin = j.SalaryMin,
                        SalaryMax = j.SalaryMax,

                        // Hybrid coordinates logic: Use JobPost coordinates if available, otherwise use Location coordinates
                        Latitude = j.Latitude ?? (j.Location != null ? j.Location.Latitude : null),
                        Longitude = j.Longitude ?? (j.Location != null ? j.Location.Longitude : null),
                        CoordinateSource = j.Latitude.HasValue && j.Longitude.HasValue ? "JobPost" : "Location",

                        Created = j.Created,
                        CountryName = j.Country.CountryName,
                        LocationId = j.LocationId ?? 0,
                        CompanyName = j.Company != null ? j.Company.CompanyName : null,
                        LocationName = j.Location != null ? j.Location.LocationName : null,
                        ContractType = j.ContractType != null ? j.ContractType.Type : null,
                        ContractTime = j.ContractTime != null ? j.ContractTime.Time : null,
                        WorkLocation = j.WorkplaceModel != null ? j.WorkplaceModel.Workplace : null,
                        Skills = j.JobPostSkills.Select(js => js.Skill.SkillName).ToList(),
                        Languages = j.JobPostLanguages.Select(jl => jl.Language.Name).ToList()
                    })
                    .ToListAsync(cancellationToken);
            }

            // Group by location if requested
            var locationGroups = new List<JobPostLocationGroupDTO>();

            if (request.GroupByLocation)
            {
                if (request.SummaryMode)
                {
                    // For summary mode, get location info and counts without loading job details
                    var locationSummary = await query
                        .GroupBy(j => new {
                            LocationId = j.LocationId ?? 0,
                            LocationName = j.Location != null ? j.Location.LocationName : "Unknown",
                            Latitude = j.Location != null ? j.Location.Latitude : null,
                            Longitude = j.Location != null ? j.Location.Longitude : null
                        })
                        .Select(g => new JobPostLocationGroupDTO
                        {
                            LocationId = g.Key.LocationId,
                            LocationName = g.Key.LocationName,
                            Latitude = g.Key.Latitude,
                            Longitude = g.Key.Longitude,
                            JobCount = g.Count(),
                            JobPosts = new List<JobPostWithCoordinatesDTO>() // Empty in summary mode
                        })
                        .OrderBy(lg => lg.LocationName)
                        .ToListAsync(cancellationToken);

                    locationGroups = locationSummary;
                }
                else
                {
                    var grouped = jobPosts
                        .GroupBy(jp => new { jp.LocationId, jp.LocationName })
                        .Select(g =>
                        {
                            // For group coordinates, prioritize Location coordinates, but fall back to JobPost if needed
                            var firstJobWithCoords = g.FirstOrDefault(j => j.Latitude.HasValue && j.Longitude.HasValue);
                            var locationCoords = g.Where(j => j.CoordinateSource == "Location").FirstOrDefault();

                            return new JobPostLocationGroupDTO
                            {
                                LocationId = g.Key.LocationId,
                                LocationName = g.Key.LocationName ?? "Unknown",
                                // Prefer location-based coordinates for the group marker
                                Latitude = locationCoords?.Latitude ?? firstJobWithCoords?.Latitude,
                                Longitude = locationCoords?.Longitude ?? firstJobWithCoords?.Longitude,
                                JobCount = g.Count(),
                                JobPosts = g.OrderByDescending(jp => jp.Created).ToList()
                            };
                        })
                        .OrderBy(lg => lg.LocationName)
                        .ToList();

                    locationGroups = grouped;
                }
            }
            else
            {
                // If not grouping, create individual groups for each job post
                locationGroups = jobPosts.Select(jp => new JobPostLocationGroupDTO
                {
                    LocationId = jp.LocationId,
                    LocationName = jp.LocationName ?? "Unknown",
                    Latitude = jp.Latitude,
                    Longitude = jp.Longitude,
                    JobCount = 1,
                    JobPosts = new List<JobPostWithCoordinatesDTO> { jp }
                }).ToList();
            }

            return new JobPostsWithCoordinatesResultDTO
            {
                LocationGroups = locationGroups,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
            };
        }
    }
}
