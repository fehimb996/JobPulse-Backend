using JobPosts.Models;

namespace JobPosts.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(ApplicationUser user, IList<string> roles);
}