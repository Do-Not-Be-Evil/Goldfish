using Goldfish.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Goldfish.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToProblem(this ControllerBase controller, Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        return controller.Problem(statusCode: statusCode, title: error.Code, detail: error.Description);
    }
}
