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
    /// Search a RAG (Retrieval-Augmented Generation) vector database
    /// </summary>
    RAG_Search = 3,

    /// <summary>
    /// Save all or part of LLM context to a vector database (reserved for future use)
    /// </summary>
    RAG_Save = 4,

    /// <summary>
    /// Delete from vector database (reserved for future use)
    /// </summary>
    RAG_Delete = 5,

    /// <summary>
    /// Update vector database (reserved for future use)
    /// </summary>
    RAG_Update = 6
}
