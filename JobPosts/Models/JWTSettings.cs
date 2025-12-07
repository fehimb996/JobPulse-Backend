namespace JobPosts.Models;

public class JWTSettings
{
    public string? Key { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
    public double TokenExpirationMinutes { get; set; } = 60;
}