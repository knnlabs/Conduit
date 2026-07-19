namespace ConduitLLM.Core.Http;

/// <summary>
/// Operation-class identifiers attached to provider HTTP requests (via
/// <see cref="HttpRequestMessage.Options"/>) so the resilience pipeline in
/// ConduitLLM.Providers can select per-class timeout budgets.
/// </summary>
/// <remarks>
/// Lives in Core (not Providers) because Core utilities like <c>HttpClientHelper</c> tag
/// requests, while the Providers pipeline reads the tag. The Providers-side
/// <c>ConduitHttpOptions</c> exposes the strongly-typed options key built from
/// <see cref="KeyName"/>.
/// </remarks>
public static class ConduitOperationClasses
{
    /// <summary>Name of the <see cref="HttpRequestOptionsKey{TValue}"/> carrying the class.</summary>
    public const string KeyName = "Conduit.OperationClass";

    /// <summary>Interactive chat/completions/embeddings.</summary>
    public const string Chat = "chat";

    /// <summary>Chat streaming (SSE).</summary>
    public const string ChatStream = "chat-stream";

    /// <summary>Image generation.</summary>
    public const string Images = "images";

    /// <summary>Authentication/key verification.</summary>
    public const string Auth = "auth";

    /// <summary>Video generation.</summary>
    public const string Video = "video";
}
