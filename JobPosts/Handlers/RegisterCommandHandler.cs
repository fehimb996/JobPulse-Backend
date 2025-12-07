using AutoMapper;
using JobPosts.Commands;
using JobPosts.Data;
using JobPosts.DTOs;
using JobPosts.Models;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Data;

namespace JobPosts.Handlers;

public class RegisterCommandHandler(UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IOptions<EmailSettings> emailSettings,
    JobPostsDbContext context,
    IMapper mapper) : IRequestHandler<RegisterCommand, UserDto>
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IEmailService _emailService = emailService;
    private readonly EmailSettings _emailSettings = emailSettings.Value;
    private readonly JobPostsDbContext _context = context;
    private readonly IMapper _mapper = mapper;

    public async Task<UserDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = _mapper.Map<ApplicationUser>(request);
            user.UserName = user.Email;
            user.CreatedAt = DateTime.UtcNow;
            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                if (result.Errors.Any(e => e.Code == "DuplicateUserName"))
                {
                    var duplicateUserNameErrorMessage = string.Join("; ", result.Errors.Where(e => e.Code == "DuplicateUserName").Select(e => e.Description));
                    throw new InvalidOperationException(duplicateUserNameErrorMessage);
                }

                var errorMessage = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException(errorMessage);
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = new UriBuilder(_emailSettings.FrontendBaseUrl)
            {
                Path = "/confirm-email",
                Query = $"userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}"
            }.ToString();

            Console.WriteLine("token" + " " + token);
            if (token == null)
            {
                throw new InvalidOperationException("Password reset token could not be generated");
            }

            if (user.Email == null)
            {
                throw new InvalidOperationException("Email is missing.");
            }

            await _emailService.SendConfirmationEmailAsync(user.Email, confirmationLink);

            await transaction.CommitAsync(cancellationToken);

            return _mapper.Map<UserDto>(user);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
