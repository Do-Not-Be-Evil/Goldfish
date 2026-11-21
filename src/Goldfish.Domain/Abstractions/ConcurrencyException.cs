namespace Goldfish.Domain.Abstractions;

/// <summary>Thrown by the unit of work when a row changed since it was read.</summary>
public sealed class ConcurrencyException(string message, Exception inner) : Exception(message, inner);
