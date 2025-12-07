using JobPosts.Commands.Careerjet;
using JobPosts.Commands.JobPosts;
using JobPosts.Hangfire;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JobPosts.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly AdzunaJobRunner _adzunaRunner;
        private readonly CareerjetJobRunner _careerjetRunner;
        private readonly CombinedJobRunner _combinedRunner;
        private readonly IMediator _mediator;

        public JobsController(
            AdzunaJobRunner adzunaRunner,
            CareerjetJobRunner careerjetRunner,
            CombinedJobRunner combinedRunner,
            IMediator mediator)
        {
            _adzunaRunner = adzunaRunner;
            _careerjetRunner = careerjetRunner;
            _combinedRunner = combinedRunner;
            _mediator = mediator;
        }

        [HttpPost("run-adzuna")]
        public async Task<IActionResult> RunAdzuna()
        {
            await _adzunaRunner.RunAllCountriesAsync();
            return Ok("Adzuna job run completed.");
        }

        [HttpPost("run-careerjet")]
        public async Task<IActionResult> RunCareerjet()
        {
            await _careerjetRunner.RunAllCountriesAsync();
            return Ok("Careerjet job run completed.");
        }

        [HttpPost("run-all")]
        public async Task<IActionResult> RunAllJobSources()
        {
            await _combinedRunner.RunAllJobSourcesAsync();
            return Ok("Combined job run completed (Adzuna + Careerjet).");
        }
    }
}
