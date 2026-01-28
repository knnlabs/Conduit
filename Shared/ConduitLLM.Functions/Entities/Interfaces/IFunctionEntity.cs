namespace ConduitLLM.Functions.Entities.Interfaces;

/// <summary>
/// Marker interface for function-related entities with a typed primary key.
/// This mirrors IEntity from ConduitLLM.Configuration to avoid circular dependencies.
/// </summary>
/// <typeparam name="TKey">The type of the primary key (e.g., int, Guid)</typeparam>
public interface IFunctionEntity<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    TKey Id { get; set; }
}
