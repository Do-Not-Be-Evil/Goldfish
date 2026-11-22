using Goldfish.Api.Authentication;
using Goldfish.Api.Extensions;
using Goldfish.Application.Policies;
using Goldfish.Application.Policies.CreatePolicy;
using Goldfish.Application.Policies.GetPolicyById;
using Goldfish.Application.Policies.ListPolicies;
using Goldfish.Application.Policies.UpdatePolicy;
using Goldfish.Application.Quotes;
using Goldfish.Application.Quotes.CreateQuote;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Goldfish.Api.Controllers;

[ApiController]
[Route("api/policies")]
public sealed class PoliciesController : ControllerBase
{
    private string CallerName =>
        User.Identity?.Name ?? throw new InvalidOperationException("Authenticated caller has no name.");

    [HttpPost]
    [Authorize(Policy = Permissions.PoliciesWrite)]
    [ProducesResponseType<PolicyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        CreatePolicyRequest request,
        [FromServices] CreatePolicyHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreatePolicyCommand(request.PolicyType, request.CoverageLimit, request.BasePrice, CallerName),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        Response.Headers.ETag = result.Value.ETag;
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet]
    [Authorize(Policy = Permissions.PoliciesRead)]
    [ProducesResponseType<PolicyPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromServices] ListPoliciesHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] int skip = 0,
        [FromQuery] int take = ListPoliciesQuery.DefaultTake,
        [FromQuery] string? policyType = null)
    {
        var result = await handler.HandleAsync(new ListPoliciesQuery(skip, take, policyType), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.PoliciesRead)]
    [ProducesResponseType<PolicyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] GetPolicyByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetPolicyByIdQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        Response.Headers.ETag = result.Value.ETag;
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.PoliciesWrite)]
    [ProducesResponseType<PolicyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdatePolicyRequest request,
        [FromServices] UpdatePolicyHandler handler,
        CancellationToken cancellationToken)
    {
        var ifMatch = Request.Headers[HeaderNames.IfMatch].ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return Problem(statusCode: StatusCodes.Status428PreconditionRequired,
                title: "Policy.IfMatchRequired", detail: "Send the policy's current ETag in the If-Match header.");
        }

        if (!PolicyETag.TryParse(ifMatch, out var expectedRowVersion))
        {
            return Problem(statusCode: StatusCodes.Status412PreconditionFailed,
                title: "Policy.Concurrency", detail: "The If-Match header is not a valid policy ETag.");
        }

        var result = await handler.HandleAsync(
            new UpdatePolicyCommand(id, request.PolicyType, request.CoverageLimit, request.BasePrice, expectedRowVersion, CallerName),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        Response.Headers.ETag = result.Value.ETag;
        return Ok(result.Value);
    }

    [HttpPost("{policyId:guid}/quotes")]
    [Authorize(Policy = Permissions.QuotesCreate)]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateQuote(
        Guid policyId,
        [FromServices] CreateQuoteHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CreateQuoteCommand(policyId, CallerName), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(QuotesController.GetById), "Quotes", new { id = result.Value.Id }, result.Value)
            : this.ToProblem(result.Error);
    }
}

public sealed record CreatePolicyRequest(string PolicyType, decimal CoverageLimit, decimal BasePrice);

public sealed record UpdatePolicyRequest(string PolicyType, decimal CoverageLimit, decimal BasePrice);
