using Goldfish.Application.Products;
using Goldfish.Application.Products.CreateProduct;
using Goldfish.Application.Products.GetProductById;
using Goldfish.Domain.Products;
using Microsoft.AspNetCore.Mvc;

namespace Goldfish.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] GetProductByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetProductByIdQuery(id), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(statusCode: StatusCodes.Status404NotFound, title: result.Error.Code, detail: result.Error.Description);
    }

    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        CreateProductRequest request,
        [FromServices] CreateProductHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CreateProductCommand(request.Name, request.Price), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error.Code, detail: result.Error.Description);
    }
}

public sealed record CreateProductRequest(string Name, decimal Price);
