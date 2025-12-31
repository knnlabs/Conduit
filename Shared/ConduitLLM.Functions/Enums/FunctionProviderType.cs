namespace ConduitLLM.Functions.Enums;

/// <summary>
/// Defines the types of function providers available in the system.
/// Each provider type represents a different external service or API.
/// </summary>
public enum FunctionProviderType
{
    /// <summary>
    /// Exa.ai - Neural and keyword-based web search
    /// </summary>
    Exa = 1,

    /// <summary>
    /// Perplexity AI - Question answering and search (future)
    /// </summary>
    Perplexity = 2,

    /// <summary>
    /// Custom RAG implementation (future)
    /// </summary>
    CustomRAG = 3,

    /// <summary>
    /// Tavily search API - RAG-optimized search with structured results
    /// </summary>
    Tavily = 4,

    /// <summary>
    /// Custom/extensibility provider
    /// </summary>
    Custom = 99
}
