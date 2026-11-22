using System.Text.Json;
using Goldfish.Application.RulesAdmin;
using Goldfish.Domain.RulesAdmin;
using Goldfish.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Goldfish.Web.Pages.Changes;

/// <summary>
/// Edits the rules of a draft. rule-editor.ts adds and removes rows and shows ratios as the analyst types;
/// without script the Add/Remove buttons post here instead. Saving always validates on the server.
/// </summary>
public sealed class EditModel(GetChangeRequestHandler get, SaveRevisionHandler save, IRulesAdminStore store) : PageModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [FromRoute] public Guid Id { get; set; }

    [BindProperty] public int ExpectedRevision { get; set; }
    [BindProperty] public string? Action { get; set; }
    [BindProperty] public List<RuleRow> Rules { get; set; } = [];

    public ChangeRequestDetails Details { get; private set; } = default!;
    public IReadOnlyList<RuleSetProblem> Problems { get; private set; } = [];
    public IReadOnlyList<RuleChange> Changes { get; private set; } = [];
    public string? Error { get; private set; }

    /// <summary>Base-version multipliers and the band, for rule-editor.ts's live ratio column.</summary>
    public string ClientData { get; private set; } = "{}";

    public sealed class RuleRow
    {
        public string? Name { get; set; }
        public int? Priority { get; set; }
        public string? PolicyType { get; set; }
        public decimal? MinCoverageLimitExclusive { get; set; }
        public decimal? MaxCoverageLimitInclusive { get; set; }
        public decimal? Multiplier { get; set; }
        public string? Reason { get; set; }

        public static RuleRow From(DraftRule r) => new()
        {
            Name = r.Name, Priority = r.Priority, PolicyType = r.PolicyType,
            MinCoverageLimitExclusive = r.MinCoverageLimitExclusive, MaxCoverageLimitInclusive = r.MaxCoverageLimitInclusive,
            Multiplier = r.Multiplier, Reason = r.Reason,
        };

        public DraftRule ToDraftRule() => new(
            Name?.Trim() ?? string.Empty,
            Priority ?? 0,
            string.IsNullOrWhiteSpace(PolicyType) ? null : PolicyType,
            MinCoverageLimitExclusive,
            MaxCoverageLimitInclusive,
            Multiplier ?? 0m,
            string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim());
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();
        if (!Details.Actions.CanEdit) return Page();

        ExpectedRevision = Details.Data.LatestRevision.RevisionNumber;
        Rules = Details.Data.LatestRevision.Rules.Select(RuleRow.From).ToList();
        await RecalculateAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();
        if (!Details.Actions.CanEdit) return Forbid();

        ModelState.Clear(); // problems are reported from RuleSetValidator, row by row
        var action = Action ?? "check";
        if (action == "add")
        {
            var next = Rules.Where(r => r.Priority is not null).Select(r => r.Priority!.Value).DefaultIfEmpty(0).Max() + 10;
            Rules.Add(new RuleRow { Priority = next, Multiplier = 1m });
        }
        else if (action.StartsWith("remove:", StringComparison.Ordinal)
                 && int.TryParse(action["remove:".Length..], out var index) && index >= 0 && index < Rules.Count)
        {
            Rules.RemoveAt(index);
        }

        await RecalculateAsync(cancellationToken);
        if (action != "save") return Page();

        if (Problems.Count > 0)
        {
            Error = RulesAdminErrors.Invalid.Description;
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Page();
        }

        var result = await save.HandleAsync(Id, WebRoles.ToActor(User), ExpectedRevision,
            Rules.Select(r => r.ToDraftRule()).ToList(), cancellationToken);
        if (result.IsFailure)
        {
            Error = result.Error.Description;
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Page();
        }

        return RedirectToPage("Details", new { id = Id });
    }

    public IEnumerable<RuleSetProblem> ProblemsFor(int row) => Problems.Where(p => p.Row == row);

    public RuleChange? ChangeFor(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null
            : Changes.FirstOrDefault(c => c.New is not null && string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var result = await get.HandleAsync(Id, WebRoles.ToActor(User), cancellationToken);
        if (result.IsFailure) return false;
        Details = result.Value;
        return true;
    }

    private async Task RecalculateAsync(CancellationToken cancellationToken)
    {
        var rules = Rules.Select(r => r.ToDraftRule()).ToList();
        Problems = RuleSetValidator.Validate(rules);

        var baseRules = await store.GetVersionRulesAsync(Details.Data.LatestRevision.BaseVersionNumber, cancellationToken);
        Changes = RuleSetDiff.Compute(baseRules,
            rules.Where(r => r.Name.Length > 0).DistinctBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            Details.Band);

        ClientData = JsonSerializer.Serialize(new
        {
            band = new { min = Details.Band.MinRatio, max = Details.Band.MaxRatio },
            baseRules = baseRules.Select(r => new { name = r.Name, multiplier = r.Multiplier }),
        }, Json);
    }
}
