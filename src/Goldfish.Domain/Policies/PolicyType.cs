namespace Goldfish.Domain.Policies;

/// <summary>Closed list of policy types. Must match the CHECK constraints in the database.</summary>
public enum PolicyType
{
    Commercial,
    Personal
}
