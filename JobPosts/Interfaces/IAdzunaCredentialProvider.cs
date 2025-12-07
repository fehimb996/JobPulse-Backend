using JobPosts.Models;

namespace JobPosts.Providers
{
    public interface IAdzunaCredentialProvider
    {
        AdzunaCredential? GetNextCredential();
        void MarkCredentialAsExhausted(AdzunaCredential credential);
        void ResetExhausted();
    }
} 