using JobPosts.DTOs;

namespace JobPosts.Interfaces
{
    public interface IAdzunaService
    {
        Task<AdzunaJobDTO?> GetJobDetailsAsync(long jobId);
    }
}
