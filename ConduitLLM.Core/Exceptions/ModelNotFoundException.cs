using System;

namespace ConduitLLM.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a requested model is not found in the system configuration.
    /// Maps to HTTP 404 Not Found status code.
    /// </summary>
    public class ModelNotFoundException : ConduitException
    {
        /// <summary>
        /// Gets the name of the model that was not found.
        /// </summary>
        public string ModelName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelNotFoundException"/> class.
        /// </summary>
        /// <param name="modelName">The name of the model that was not found.</param>
        public ModelNotFoundException(string modelName) 
            : base($"Model '{modelName}' not found. Please check your model configuration.")
        {
            ModelName = modelName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelNotFoundException"/> class with a custom message.
        /// </summary>
        /// <param name="modelName">The name of the model that was not found.</param>
        /// <param name="message">A custom error message.</param>
        public ModelNotFoundException(string modelName, string message) 
            : base(message)
        {
            ModelName = modelName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelNotFoundException"/> class with an inner exception.
        /// </summary>
        /// <param name="modelName">The name of the model that was not found.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ModelNotFoundException(string modelName, string message, Exception innerException) 
            : base(message, innerException)
        {
            ModelName = modelName;
        }

        // TODO: Future enhancement - Add GetSimilarModels() method for suggestions using Levenshtein distance
        // TODO: Future enhancement - Cache model list for faster validation
    }
}