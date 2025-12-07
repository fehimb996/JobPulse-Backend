using JobPosts.Commands.FavoriteJobs;
using JobPosts.Queries.FavoriteJobs;
using JobPosts.Data;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace JobPosts.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly IMediator _mediator;

    public FavoritesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    [HttpPost("add-to-favorites")]
    public async Task<IActionResult> AddFavorites([FromBody] List<int> Ids)
    {
        var userId = GetUserId();
        await _mediator.Send(new AddFavoriteJobsCommand(userId, Ids));
        return Ok();
    }

    [HttpDelete("remove-from-favorites")]
    public async Task<IActionResult> RemoveFavorites([FromBody] List<int> Ids)
    {
        var userId = GetUserId();
        await _mediator.Send(new RemoveFavoriteJobsCommand(userId, Ids));
        return NoContent();
    }

    [HttpGet("check/{Id}")]
    public async Task<IActionResult> IsFavorited(int Id)
    {
        var userId = GetUserId();
        var isFavorite = await _mediator.Send(new IsJobFavoritedQuery(userId, Id));
        return Ok(new { isFavorite });
    }

    [HttpGet("get-all-favorites")]
    public async Task<IActionResult> GetFavorites()
    {
        var userId = GetUserId();
        var favorites = await _mediator.Send(new GetFavoriteJobsQuery(userId));
        return Ok(favorites);
    }
}
