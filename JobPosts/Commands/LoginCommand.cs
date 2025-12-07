using JobPosts.DTOs;
using MediatR;

namespace JobPosts.Commands;

public class LoginCommand : IRequest<LoginResponse>
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}