using JobPosts.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace JobPosts.Configurations;

public static class JwtConfig
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JWTSettings>(configuration.GetSection("JWT"));

        var jwtSettings = configuration.GetSection("JWT").Get<JWTSettings>() ?? throw new ArgumentNullException("JWT Settings is missing in configuration.");

        var key = Encoding.UTF8.GetBytes(jwtSettings.Key ?? throw new ArgumentNullException("JWT Key is missing in configuration."));

        services.AddAuthentication(x =>
        {
            x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(opt =>
        {
            opt.RequireHttpsMetadata = true;
            opt.SaveToken = true;
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = Convert.ToBoolean(jwtSettings.ValidateIssuer),
                ValidateAudience = Convert.ToBoolean(jwtSettings.ValidateAudience),
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };
            opt.Events = CreateCommonJwtEvents();
        });

        return services;
    }

    public static JwtBearerEvents CreateCommonJwtEvents()
    {
        return new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/orders"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                context.HttpContext.Items["AuthenticationError"] = context.Exception;

                context.HttpContext.Items["AuthenticationErrorMessage"] = context.Exception switch
                {
                    SecurityTokenExpiredException => "Token has expired.",
                    SecurityTokenInvalidSignatureException => "Invalid token signature.",
                    SecurityTokenInvalidIssuerException => "Invalid token issuer.",
                    SecurityTokenInvalidAudienceException => "Invalid token audience.",
                    SecurityTokenNoExpirationException => "Token has no expiration.",
                    SecurityTokenNotYetValidException => "Token is not yet valid.",
                    SecurityTokenInvalidLifetimeException => "Token has invalid lifetime.",
                    SecurityTokenValidationException => "Token validation failed.",
                    ArgumentException or FormatException => "Invalid token format.",
                    _ => "Authentication failed."
                };

                return Task.CompletedTask;
            }
        };
    }
}
