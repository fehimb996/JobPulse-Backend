using JobPosts.Commands.Careerjet;
using JobPosts.Hangfire;
using JobPosts.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JobPosts.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CareerjetController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly CareerjetJobRunner _runner;
        private readonly ILogger<CareerjetController> _logger;

        public CareerjetController(
            IMediator mediator,
            CareerjetJobRunner runner,
            ILogger<CareerjetController> logger)
        {
            _mediator = mediator;
            _runner = runner;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves and saves unique data from Careerjet API. Supported countries: NO (Norway), DK (Denmark)
        /// </summary>
        /// <param name="country">Country code (NO, DK)</param>
        /// <returns>Fetch results summary</returns>
        [HttpPost("fetch")]
        public async Task<IActionResult> Fetch([FromQuery] string country)
        {
            if (string.IsNullOrWhiteSpace(country))
            {
                return BadRequest(new { error = "Country code is required. Supported: NO, DK" });
            }

            try
            {
                var command = new FetchCareerjetJobsCommand(country);
                var result = await _mediator.Send(command);

                return Ok(new
                {
                    Country = country.ToUpperInvariant(),
                    TotalJobs = result.TotalJobs,
                    TotalPages = result.TotalPages,
                    SavedJobs = result.SavedJobs
                });
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Careerjet data for country: {Country}", country);
                return StatusCode(500, new { error = "Failed to fetch data", details = ex.Message });
            }
        }

        /// <summary>
        /// Runs Careerjet job fetch for all supported countries (NO, DK)
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPost("run-all")]
        public async Task<IActionResult> RunAll()
        {
            try
            {
                await _runner.RunAllCountriesAsync();
                return Ok(new { message = "Careerjet job fetch completed for all countries" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run Careerjet job fetch for all countries");
                return StatusCode(500, new { error = "Failed to run job fetch", details = ex.Message });
            }
        }
    }
}
