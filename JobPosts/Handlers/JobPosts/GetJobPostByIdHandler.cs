using JobPosts.Data;
using JobPosts.DTOs.JobPosts;
using JobPosts.Queries.JobPosts;
using MediatR;
using System;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers.JobPosts 
{
    public class GetJobPostByIdHandler : IRequestHandler<GetJobPostByIdQuery, JobPostDetailsDTO?>
    {
        private readonly JobPostsDbContext _context;

        public GetJobPostByIdHandler(JobPostsDbContext context)
        {
            _context = context;
        }

        public async Task<JobPostDetailsDTO?> Handle(GetJobPostByIdQuery request, CancellationToken cancellationToken)
        {
            var job = await _context.JobPosts
                .Include(j => j.Country)
                .Include(j => j.Company)
                .Include(j => j.Location)
                .Include(j => j.ContractType)
                .Include(j => j.ContractTime)
                .Include(j => j.WorkplaceModel)
                .Include(j => j.JobPostSkills).ThenInclude(js => js.Skill)
                .Include(j => j.JobPostLanguages).ThenInclude(jl => jl.Language)
                .FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken);

            if (job == null) return null;

            return new JobPostDetailsDTO
            {
                Id = job.Id,
                Title = job.Title,
                Description = job.Description,
                FullDescription = job.FullDescription,
                Url = job.Url,
                SalaryMin = job.SalaryMin,
                SalaryMax = job.SalaryMax,
                Created = job.Created,
                CountryName = job.Country.CountryName,
                CountryCode = job.Country.CountryCode,
                CompanyName = job.Company?.CompanyName,
                CompanyUrl = job.Company?.Url,
                LocationName = job.Location?.LocationName,
                ContractType = job.ContractType?.Type,
                ContractTime = job.ContractTime?.Time,
                WorkplaceModel = job.WorkplaceModel?.Workplace,
                Skills = job.JobPostSkills.Select(s => s.Skill.SkillName).ToList(),
                Languages = job.JobPostLanguages.Select(l => l.Language.Name).ToList()
            };
        }
    }
}
