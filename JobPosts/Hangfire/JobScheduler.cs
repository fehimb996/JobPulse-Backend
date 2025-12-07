using Hangfire;

namespace JobPosts.Hangfire
{
    public class JobScheduler
    {
        public static void RegisterRecurringJobs()
        {
            var times = new[] { 22, 6, 16 };

            foreach (var hour in times)
            {
                // Register combined job that runs Adzuna first, then Careerjet
                RecurringJob.AddOrUpdate<CombinedJobRunner>(
                    $"combined-jobs-{hour:D2}",
                    job => job.RunAllJobSourcesAsync(),
                    $"0 {hour} * * *"
                );
            }

            // Add daily cleanup job at 2 AM
            RecurringJob.AddOrUpdate<JobPostCleanupRunner>(
                "cleanup-old-jobposts",
                job => job.RunCleanupAsync(),
                "0 2 * * *" // Daily at 2:00 AM
            );

            //// Optional: Keep individual runners for manual execution if needed
            //// You can remove these if you only want the combined approach
            //foreach (var hour in times)
            //{
            //    RecurringJob.AddOrUpdate<AdzunaJobRunner>(
            //        $"adzuna-job-{hour:D2}",
            //        job => job.RunAllCountriesAsync(),
            //        $"0 {hour} * * *"
            //    );
            //}
        }
    }
}
