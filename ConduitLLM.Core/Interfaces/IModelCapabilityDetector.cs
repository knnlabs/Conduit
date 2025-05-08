using System.Collections.Generic;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for detecting and validating model capabilities, particularly for 
    /// specialized features like vision/multimodal support.
    /// </summary>
    public interface IModelCapabilityDetector
    {
        /// <summary>
        /// Determines if a model has vision (image processing) capabilities.
        /// </summary>
        /// <param name="modelName">The name of the model to check</param>
        /// <returns>True if the model supports vision input, false otherwise</returns>
        bool HasVisionCapability(string modelName);
        
        /// <summary>
        /// Determines if a chat completion request contains image content that 
        /// requires a vision-capable model.
        /// </summary>
        /// <param name="request">The chat completion request to check</param>
        /// <returns>True if the request contains image content, false otherwise</returns>
        bool ContainsImageContent(ChatCompletionRequest request);
        
        /// <summary>
        /// Gets a list of all available models that support vision capabilities.
        /// </summary>
        /// <returns>A collection of model names that support vision</returns>
        IEnumerable<string> GetVisionCapableModels();
        
        /// <summary>
        /// Validates that a request can be processed by the specified model.
        /// </summary>
        /// <param name="request">The chat completion request to validate</param>
        /// <param name="modelName">The name of the model to check</param>
        /// <param name="errorMessage">Error message if validation fails</param>
        /// <returns>True if the request is valid for the model, false otherwise</returns>
        bool ValidateRequestForModel(ChatCompletionRequest request, string modelName, out string errorMessage);
    }
}