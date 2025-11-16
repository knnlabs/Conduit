using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services.Strategies
{
    /// <summary>
    /// Strategy interface for processing different media formats.
    /// </summary>
    /// <typeparam name="TMediaData">The type of media data to process</typeparam>
    public interface IMediaProcessingStrategy<in TMediaData>
        where TMediaData : class
    {
        /// <summary>
        /// Processes media data and stores it appropriately.
        /// </summary>
        /// <param name="mediaData">The media data to process</param>
        /// <param name="context">Processing context with metadata</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Processed media information</returns>
        Task<ProcessedMediaItem> ProcessAsync(
            TMediaData mediaData,
            MediaProcessingContext context,
            CancellationToken cancellationToken);
    }
}