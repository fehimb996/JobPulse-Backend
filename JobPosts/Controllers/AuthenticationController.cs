using JobPosts.Commands;
using JobPosts.Data;
using JobPosts.DTOs;
using JobPosts.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using JobPosts.Handlers;
using JobPosts.DTOs.JobPosts;

namespace JobPosts.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthenticationController(IMediator _mediator) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterCommand command)
    {
        try
        {
            var userDto = await _mediator.Send(command);
            return CreatedAtAction(nameof(RegisterUser), new { id = userDto.ID }, userDto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand request)
    {
        try
        {
            var result = await _mediator.Send(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailCommand command)
    {
        try
        {
            var resultMessage = await _mediator.Send(command);
            return Ok(resultMessage);
        }
        catch (UserNotFoundException)
        {
            return BadRequest("Invalid user");
        }
        catch (EmailAlreadyConfirmedException)
        {
            return BadRequest("Email already confirmed.");
        }
        catch (EmailConfirmationFailedException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize]
    [HttpGet("get-logged-in-user")]
    public async Task<IActionResult> GetLoggedInUser([FromServices] JobPostsDbContext context)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        // Project favorites directly from the join table to DTOs.
        var favorites = await context.UserFavoriteJobs
            .AsNoTracking()
            .Where(ufj => ufj.UserId == userId)
            .Select(ufj => ufj.JobPost)            // navigation to JobPost
            .Where(j => j != null)
            .OrderByDescending(j => j!.Created)
            .Select(j => new JobPostDTO
            {
                Id = j!.Id,
                Title = j.Title,
                Description = j.Description,
                Url = j.Url,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                Created = j.Created,
                CountryName = j.Country != null ? j.Country.CountryName : null,
                CompanyName = j.Company != null ? j.Company.CompanyName : null,
                LocationName = j.Location != null ? j.Location.LocationName : null,
                ContractType = j.ContractType != null ? j.ContractType.Type : null,
                ContractTime = j.ContractTime != null ? j.ContractTime.Time : null,
                WorkLocation = j.WorkplaceModel != null ? j.WorkplaceModel.Workplace : null,
                Skills = j.JobPostSkills != null ? j.JobPostSkills.Select(js => js.Skill.SkillName).ToList() : new List<string>(),
                Languages = j.JobPostLanguages != null ? j.JobPostLanguages.Select(jl => jl.Language.Name).ToList() : new List<string>()
            })
            .ToListAsync();

        // Load light user info separately
        var user = await context.Users
            .AsNoTracking()
            .Select(u => new { u.Id, u.Email, u.Name, u.Surname, u.ProfileImageUrl })
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound("User not found.");

        var result = new LoggedInUserDto
        {
            Id = user.Id,
            Email = user.Email!,
            Name = user.Name,
            Surname = user.Surname,
            ProfileImageUrl = user.ProfileImageUrl,
            Favorites = favorites
        };

        return Ok(result);
    }
}
