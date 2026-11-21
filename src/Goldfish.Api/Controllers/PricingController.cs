using Goldfish.Api.Authentication;
using Goldfish.Api.Extensions;
using Goldfish.Application.Pricing.GetActiveVersion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Goldfish.Api.Controllers;

[ApiController]
[Route("api/pricing")]
public sealed class PricingController : ControllerBase
{
    [HttpGet("active-version")]
    [Authorize(Policy = Permissions.PricingRead)]
    [ProducesResponseType<ActiveVersionStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetActiveVersion(
        [FromServices] GetActiveVersionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }
}
