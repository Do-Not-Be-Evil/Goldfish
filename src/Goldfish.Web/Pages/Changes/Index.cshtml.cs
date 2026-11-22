using Goldfish.Application.RulesAdmin;
using Goldfish.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Goldfish.Web.Pages.Changes;

public sealed class IndexModel(ListChangeRequestsHandler list, CreateChangeRequestHandler create) : PageModel
{
    public Actor Actor => WebRoles.ToActor(User);

    public IReadOnlyList<ChangeRequestSummary> Requests { get; private set; } = [];

    [BindProperty] public string? Title { get; set; }

    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Requests = await list.HandleAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var result = await create.HandleAsync(Actor, Title, cancellationToken);
        if (result.IsSuccess)
        {
            return RedirectToPage("Edit", new { id = result.Value });
        }

        Error = result.Error.Description;
        Requests = await list.HandleAsync(cancellationToken);
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }
}
