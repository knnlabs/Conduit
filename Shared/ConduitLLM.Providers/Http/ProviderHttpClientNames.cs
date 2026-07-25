using ConduitLLM.Configuration;

namespace ConduitLLM.Providers.Http;

/// <summary>
/// Single source of truth for the named-HttpClient strings used for LLM provider traffic.
/// </summary>
/// <remarks>
/// <para>
/// Provider clients request named HttpClients at runtime (see <c>BaseLLMClient.CreateHttpClient</c>,
/// which builds <c>$"{ProviderName}LLMClient"</c>), and DI registration must configure those exact
/// names for resilience policies to attach — <see cref="System.Net.Http.IHttpClientFactory"/>
/// silently returns an unconfigured client for any unknown name. Registration and request sides
/// must therefore derive names from this class so they can never diverge.
/// </para>
/// <para>
/// The prefixes below are pinned to the literal <c>providerName</c> strings each client passes to
/// <c>BaseLLMClient</c> today (e.g. <c>"groq"</c>, <c>"OpenAI"</c> via
/// <c>ProviderType.ToString()</c>, <c>"minimax"</c>). Casing is significant: HttpClient names are
/// case-sensitive. Do not "normalize" these values — changing a prefix silently detaches every
/// policy from that provider's traffic. The attachment regression tests in
/// <c>ProviderHttpClientRegistrationTests</c> pin the resulting names as string literals.
/// </para>
/// <para>
/// <see cref="ProviderType.Ultravox"/> and <see cref="ProviderType.ElevenLabs"/> are excluded:
/// no client implementations exist for them.
/// </para>
/// </remarks>
public static class ProviderHttpClientNames
{
    private static readonly IReadOnlyDictionary<ProviderType, string> Prefixes =
        new Dictionary<ProviderType, string>
        {
            [ProviderType.OpenAI] = "OpenAI",
            [ProviderType.Groq] = "groq",
            [ProviderType.Replicate] = "Replicate",
            [ProviderType.Fireworks] = "Fireworks",
            [ProviderType.OpenAICompatible] = "OpenAICompatible",
            [ProviderType.MiniMax] = "minimax",
            [ProviderType.Cerebras] = "cerebras",
            [ProviderType.SambaNova] = "sambanova",
            [ProviderType.DeepInfra] = "DeepInfra",
            [ProviderType.Cloudflare] = "Cloudflare",
            [ProviderType.OpenRouter] = "OpenRouter",
            [ProviderType.Meta] = "meta",
            [ProviderType.Azure] = "Azure",
            [ProviderType.Bedrock] = "bedrock",
        };

    /// <summary>
    /// Named client used for a provider's primary LLM traffic (chat, embeddings, images).
    /// </summary>
    public static string Chat(ProviderType providerType) => Prefixes[providerType] + "LLMClient";

    /// <summary>
    /// Named client used for authentication/key verification calls.
    /// </summary>
    public static string Auth(ProviderType providerType) => Prefixes[providerType] + "AuthVerification";

    /// <summary>
    /// Named client used for long-running video generation calls.
    /// </summary>
    public static string Video(ProviderType providerType) => Prefixes[providerType] + "VideoClient";

    /// <summary>
    /// Provider types that have a client implementation and therefore a registered named client.
    /// </summary>
    public static IEnumerable<ProviderType> RegisteredTypes => Prefixes.Keys;
}
