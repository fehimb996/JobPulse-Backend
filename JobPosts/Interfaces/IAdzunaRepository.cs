using JobPosts.Models;

namespace JobPosts.Interfaces
{
    public interface IAdzunaRepository
    {
        Task<Adzuna?> GetJobByIdAsync(long jobId);
    }
}
