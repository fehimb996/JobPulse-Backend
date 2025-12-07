using JobPosts.Commands;
using JobPosts.Models;
using MediatR;
using Microsoft.AspNetCore.Identity;
using System.Data;
using System.Net;
using JobPosts.Exceptions;

namespace JobPosts.Handlers;

public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, string>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ConfirmEmailCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<string> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
            throw new UserNotFoundException(request.UserId);

        if (user.EmailConfirmed)
            throw new EmailAlreadyConfirmedException();

        var decodedToken = request.Token;
        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

        if (!result.Succeeded)
        {
            var errorMessage = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new EmailConfirmationFailedException(errorMessage);
        }

        return "Email confirmed successfully.";
    }
}
