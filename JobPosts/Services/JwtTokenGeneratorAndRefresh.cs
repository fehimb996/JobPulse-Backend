using JobPosts.Interfaces;
using JobPosts.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace JobPosts.Services;

public class JwtTokenGeneratorAndRefresh(IOptions<JWTSettings> jwtSettings) : IJwtTokenGenerator
{
    private readonly JWTSettings _jwtSettings = jwtSettings.Value;

    public string GenerateToken(ApplicationUser user, IList<string> roles)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        if (string.IsNullOrEmpty(_jwtSettings.Key))
        {
            throw new ArgumentException("JWT key is missing.");
        }
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var tokenExpirationMin = _jwtSettings.TokenExpirationMinutes;
        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.UserName!),
        new Claim(ClaimTypes.NameIdentifier, user.Id),
    };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescription = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(tokenExpirationMin),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
            Issuer = _jwtSettings.Issuer,
            IssuedAt = DateTime.UtcNow,
            Audience = _jwtSettings.Audience
        };

        var token = tokenHandler.CreateToken(tokenDescription);
        return tokenHandler.WriteToken(token);
    }
}