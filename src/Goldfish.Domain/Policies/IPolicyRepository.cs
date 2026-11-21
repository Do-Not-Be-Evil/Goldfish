namespace Goldfish.Domain.Policies;

public interface IPolicyRepository
{
    /// <summary>Reads a policy without tracking.</summary>
    Task<Policy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a tracked policy whose original row version is set to <paramref name="expectedRowVersion"/>,
    /// so saving fails with a concurrency error if the row has changed since the caller read it.
    /// </summary>
    Task<Policy?> GetForUpdateAsync(Guid id, byte[] expectedRowVersion, CancellationToken cancellationToken = default);

    void Add(Policy policy);
}
