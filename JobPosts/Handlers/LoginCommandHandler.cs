using JobPosts.Commands;
using JobPosts.Data;
using JobPosts.DTOs;
using JobPosts.Interfaces;
using JobPosts.Models;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Handlers;

public class LoginCommandHandler(JobPostsDbContext context,
    UserManager<ApplicationUser> userManager,
    ILogger<LoginCommandHandler> logger,
    SignInManager<ApplicationUser> signInManager,
    IJwtTokenGenerator jwtTokenGenerator
   ) : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly JobPostsDbContext _context = context;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ILogger<LoginCommandHandler> _logger = logger;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            throw new InvalidOperationException("Email and Password are required.");

        _logger.LogInformation("Login attempt with email/username: {EmailOrUsername}", request.Email);

        var user = await _context.ApplicationUsers
            .FirstOrDefaultAsync(u => u.Email == request.Email || u.UserName == request.Email, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login failed for email/username: {EmailOrUsername} - User not found", request.Email);
            throw new InvalidOperationException($"Invalid Credentials");
        }

        var isValid = await _userManager.CheckPasswordAsync(user!, request.Password);

        if (!isValid)
        {
            _logger.LogWarning("Login failed for email/username: {EmailOrUsername} - Invalid Credentials", request.Email);
            throw new InvalidOperationException("Invalid Credentials");
        }

        if (!user.EmailConfirmed)
        {
            throw new InvalidOperationException("Email not confirmed. Please confirm your email before logging in.");
        }

        _logger.LogInformation("Login successful for email/username: {EmailOrUsername}", request.Email);

        return new LoginResponse
        {
            AccessToken = _jwtTokenGenerator.GenerateToken(user, []),
            Id = user.Id,
            Email = user.Email ?? "",
        };
    }
}