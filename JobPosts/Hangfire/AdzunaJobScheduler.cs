using Hangfire;

namespace JobPosts.Hangfire
{
    public class AdzunaJobScheduler
    {
        //public static void RegisterRecurringJobs()
        //{
        //    var times = new[] { 22, 6, 16 };

        //    foreach (var hour in times)
        //    {
        //        RecurringJob.AddOrUpdate<AdzunaJobRunner>(
        //            $"adzuna-job-{hour:D2}",
        //            job => job.RunAllCountriesAsync(),
        //            $"0 {hour} * * *"
        //        );
        //    }
        //}
    }
}
