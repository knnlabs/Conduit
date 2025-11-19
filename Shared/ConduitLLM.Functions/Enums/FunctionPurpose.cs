namespace ConduitLLM.Functions.Enums;

/// <summary>
/// Defines the purpose or intended use case for a function.
/// This categorizes functions by what they accomplish.
/// </summary>
public enum FunctionPurpose
{
    /// <summary>
    /// Search the internet or web and return results
    /// </summary>
    Search = 1,

    /// <summary>
    /// Send a question or prompt to another LLM for answering
    /// </summary>
    Answer = 2,

    /// <summary>
    /// Retrieve content from specific URLs (web scraping, content extraction)
    /// </summary>
    ContentRetrieval = 3,

    /// <summary>
    /// RAG (Retrieval-Augmented Generation) operations including search, save, update, and delete for vector databases
    /// </summary>
    RAG = 4
}
