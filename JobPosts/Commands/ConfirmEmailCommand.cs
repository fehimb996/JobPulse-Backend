using MediatR;

namespace JobPosts.Commands;

public record ConfirmEmailCommand(string UserId, string Token) : IRequest<string>;
