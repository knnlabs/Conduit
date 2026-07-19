namespace ConduitLLM.Providers.Http;

/// <summary>
/// Request-level options for provider HTTP calls, communicated via
/// <see cref="HttpRequestMessage.Options"/> to the resilience pipeline.
/// </summary>
public static class ConduitHttpOptions
{
    /// <summary>
    /// Overrides the operation class for a single request. When absent, the pipeline uses the
    /// default class of the named client the request was sent through (*LLMClient → chat,
    /// *AuthVerification → auth, *VideoClient → video). Key name and class values are defined
    /// in Core (<see cref="Core.Http.ConduitOperationClasses"/>) so Core utilities can tag
    /// requests too.
    /// </summary>
    public static readonly HttpRequestOptionsKey<string> OperationClass =
        new(Core.Http.ConduitOperationClasses.KeyName);

    /// <summary>Interactive chat/completions/embeddings.</summary>
    public const string Chat = Core.Http.ConduitOperationClasses.Chat;

    /// <summary>Chat streaming (SSE) — same budgets as chat; the attempt timeout bounds
    /// time-to-first-token because the HTTP call completes at response headers.</summary>
    public const string ChatStream = Core.Http.ConduitOperationClasses.ChatStream;

    /// <summary>Image generation.</summary>
    public const string Images = Core.Http.ConduitOperationClasses.Images;

    /// <summary>Authentication/key verification.</summary>
    public const string Auth = Core.Http.ConduitOperationClasses.Auth;

    /// <summary>Video generation.</summary>
    public const string Video = Core.Http.ConduitOperationClasses.Video;

    /// <summary>
    /// Sets the operation class on a request.
    /// </summary>
    public static HttpRequestMessage WithOperationClass(this HttpRequestMessage request, string operationClass)
    {
        request.Options.Set(OperationClass, operationClass);
        return request;
    }
}
