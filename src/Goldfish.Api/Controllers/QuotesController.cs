using Goldfish.Api.Authentication;
using Goldfish.Api.Extensions;
using Goldfish.Application.Quotes;
using Goldfish.Application.Quotes.GetQuoteById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Goldfish.Api.Controllers;

[ApiController]
[Route("api/quotes")]
public sealed class QuotesController : ControllerBase
{
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.QuotesRead)]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] GetQuoteByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetQuoteByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }
}
