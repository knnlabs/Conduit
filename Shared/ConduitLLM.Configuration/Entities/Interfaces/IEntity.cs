namespace ConduitLLM.Configuration.Entities.Interfaces;

/// <summary>
/// Marker interface for entities with a typed primary key.
/// </summary>
/// <typeparam name="TKey">The type of the primary key (e.g., int, long, Guid, string)</typeparam>
public interface IEntity<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    TKey Id { get; set; }
}

/// <summary>
/// Marker interface for entities that track creation and update timestamps.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// Gets or sets the UTC timestamp when this entity was created.
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this entity was last updated.
    /// </summary>
    DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Marker interface for entities that support soft deletion.
/// Entities implementing this interface will not be permanently deleted,
/// but instead marked with IsDeleted = true and a DeletedAt timestamp.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Gets or sets whether this entity has been soft deleted.
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this entity was soft deleted.
    /// </summary>
    DateTime? DeletedAt { get; set; }
}
