namespace ConduitLLM.Functions.Entities.Interfaces;

/// <summary>
/// Base marker interface for entities with a typed primary key.
/// Defined in the Functions project to allow shared use across projects
/// without circular dependencies.
/// </summary>
/// <typeparam name="TKey">The type of the primary key (e.g., int, Guid)</typeparam>
public interface IIdentifiableEntity<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    TKey Id { get; set; }
}
