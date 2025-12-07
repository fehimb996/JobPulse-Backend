using Hangfire;

namespace JobPosts.Hangfire
{
    public class CombinedJobRunner
    {
        private readonly AdzunaJobRunner _adzunaRunner;
        private readonly CareerjetJobRunner _careerjetRunner;
        private readonly ILogger<CombinedJobRunner> _logger;

        public CombinedJobRunner(
            AdzunaJobRunner adzunaRunner,
            CareerjetJobRunner careerjetRunner,
            ILogger<CombinedJobRunner> logger)
        {
            _adzunaRunner = adzunaRunner;
            _careerjetRunner = careerjetRunner;
            _logger = logger;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 7200)] // 2 hours timeout for combined run
        public async Task RunAllJobSourcesAsync()
        {
            _logger.LogInformation("Starting combined job fetch - Adzuna first, then Careerjet");

            try
            {
                // Run Adzuna first
                _logger.LogInformation("Starting Adzuna job fetch");
                await _adzunaRunner.RunAllCountriesAsync();
                _logger.LogInformation("Adzuna job fetch completed");

                // Add a delay between the two job sources
                await Task.Delay(2000);

                // Run Careerjet after Adzuna completes
                _logger.LogInformation("Starting Careerjet job fetch");
                await _careerjetRunner.RunAllCountriesAsync();
                _logger.LogInformation("Careerjet job fetch completed");

                _logger.LogInformation("Combined job fetch completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during combined job fetch");
                throw;
            }
        }
    }
}
