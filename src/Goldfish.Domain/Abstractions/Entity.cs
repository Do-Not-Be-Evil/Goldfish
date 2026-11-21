namespace Goldfish.Domain.Abstractions;

public abstract class Entity
{
    protected Entity(Guid id) => Id = id;

    protected Entity() { } // EF Core

    public Guid Id { get; init; }
}
