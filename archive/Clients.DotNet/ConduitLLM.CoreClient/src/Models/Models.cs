namespace ConduitLLM.CoreClient.Models;

/// <summary>
/// Represents a model available through the API.
/// </summary>
public class Model
{
    /// <summary>
    /// Gets or sets the model identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object type (always "model").
    /// </summary>
    public string Object { get; set; } = "model";

    /// <summary>
    /// Gets or sets the Unix timestamp when the model was created.
    /// </summary>
    public long Created { get; set; }

    /// <summary>
    /// Gets or sets the organization that owns the model.
    /// </summary>
    public string OwnedBy { get; set; } = string.Empty;
}

/// <summary>
/// Represents the response from the models list endpoint.
/// </summary>
public class ModelsResponse
{
    /// <summary>
    /// Gets or sets the object type (always "list").
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    /// Gets or sets the list of available models.
    /// </summary>
    public IEnumerable<Model> Data { get; set; } = new List<Model>();
}