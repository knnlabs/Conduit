using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for calculating token usage for images based on resolution and detail level.
    /// </summary>
    public interface IImageTokenCalculator
    {
        /// <summary>
        /// Calculates the token count for an image based on its URL and detail level.
        /// Uses industry-standard formulas (OpenAI's vision pricing model) for accurate estimation.
        /// </summary>
        /// <param name="imageUrl">The ImageUrl object containing the image URL and detail level</param>
        /// <returns>The estimated token count for the image</returns>
        Task<int> CalculateImageTokensAsync(ImageUrl imageUrl);
    }
}