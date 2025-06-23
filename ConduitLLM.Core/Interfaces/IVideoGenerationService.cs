using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for video generation services.
    /// Provides methods for generating videos synchronously and asynchronously.
    /// </summary>
    public interface IVideoGenerationService
    {
        /// <summary>
        /// Generates a video synchronously based on the provided request.
        /// This method blocks until the video is generated or an error occurs.
        /// </summary>
        /// <param name="request">The video generation request.</param>
        /// <param name="virtualKey">The virtual key for authentication and tracking.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The video generation response containing the generated video data.</returns>
        Task<VideoGenerationResponse> GenerateVideoAsync(
            VideoGenerationRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Initiates an asynchronous video generation task.
        /// Returns immediately with a task ID that can be used to track progress.
        /// </summary>
        /// <param name="request">The video generation request.</param>
        /// <param name="virtualKey">The virtual key for authentication and tracking.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A response containing the task ID for tracking the generation progress.</returns>
        Task<VideoGenerationResponse> GenerateVideoWithTaskAsync(
            VideoGenerationRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current status of a video generation task.
        /// </summary>
        /// <param name="taskId">The unique identifier of the generation task.</param>
        /// <param name="virtualKey">The virtual key for authentication.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current status and result (if completed) of the video generation task.</returns>
        Task<VideoGenerationResponse> GetVideoGenerationStatusAsync(
            string taskId,
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels an in-progress video generation task.
        /// </summary>
        /// <param name="taskId">The unique identifier of the generation task to cancel.</param>
        /// <param name="virtualKey">The virtual key for authentication.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the task was successfully cancelled, false otherwise.</returns>
        Task<bool> CancelVideoGenerationAsync(
            string taskId,
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates if a video generation request is supported by the specified model.
        /// </summary>
        /// <param name="request">The video generation request to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the request is valid and supported, false otherwise.</returns>
        Task<bool> ValidateRequestAsync(
            VideoGenerationRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the estimated cost for a video generation request.
        /// </summary>
        /// <param name="request">The video generation request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The estimated cost in the system's currency units.</returns>
        Task<decimal> EstimateCostAsync(
            VideoGenerationRequest request,
            CancellationToken cancellationToken = default);
    }
}