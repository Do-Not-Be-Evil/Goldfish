using System.ComponentModel.DataAnnotations;
using Goldfish.Web.Api;

namespace Goldfish.Web.Models;

/// <summary>Create and edit form. The API repeats every check; these give immediate field messages.</summary>
public sealed class PolicyFormModel : IValidatableObject
{
    public static readonly string[] PolicyTypes = ["Commercial", "Personal"];

    [Required, Display(Name = "Policy type")]
    public string? PolicyType { get; set; }

    [Required, Display(Name = "Coverage limit"), Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? CoverageLimit { get; set; }

    [Required, Display(Name = "Base price"), Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? BasePrice { get; set; }

    /// <summary>The ETag the edit started from; sent as If-Match so a concurrent change is not overwritten.</summary>
    public string? ETag { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PolicyType is not null && !PolicyTypes.Contains(PolicyType, StringComparer.Ordinal))
            yield return new("Choose Commercial or Personal.", [nameof(PolicyType)]);
        if (CoverageLimit is { } c && decimal.Round(c, 2) != c)
            yield return new("Use at most 2 decimal places.", [nameof(CoverageLimit)]);
        if (BasePrice is { } b && decimal.Round(b, 2) != b)
            yield return new("Use at most 2 decimal places.", [nameof(BasePrice)]);
    }

    public PolicyInput ToInput() => new(PolicyType!, CoverageLimit!.Value, BasePrice!.Value);

    public static PolicyFormModel From(ApiPolicy p) =>
        new() { PolicyType = p.PolicyType, CoverageLimit = p.CoverageLimit, BasePrice = p.BasePrice, ETag = p.ETag };
}

public sealed record PolicyListViewModel(ApiPolicyPage Page, string? PolicyType, bool CanEdit)
{
    public int PageNumber => Page.Skip / Page.Take + 1;
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Page.Total / (double)Page.Take));
}

public sealed record PolicyEditViewModel(Guid Id, PolicyFormModel Form, ApiPolicy? Original, ApiPolicy? Current);

public sealed record HomeViewModel(ApiActiveVersion? ActiveVersion, string? ActiveVersionProblem, int OpenChangeRequests, int AwaitingApproval);
