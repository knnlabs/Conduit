using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Http
{
    /// <summary>
    /// HTTP message handler that propagates correlation context to outbound requests.
    /// </summary>
    public class CorrelationPropagationHandler : DelegatingHandler
    {
        private readonly ICorrelationContextService _correlationService;
        private readonly ILogger<CorrelationPropagationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationPropagationHandler"/> class.
        /// </summary>
        /// <param name="correlationService">The correlation context service.</param>
        /// <param name="logger">The logger.</param>
        public CorrelationPropagationHandler(
            ICorrelationContextService correlationService,
            ILogger<CorrelationPropagationHandler> logger)
        {
            _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Get propagation headers
            var headers = _correlationService.GetPropagationHeaders();

            // Add headers to the request
            foreach (var header in headers)
            {
                // Only add if not already present
                if (!request.Headers.Contains(header.Key))
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            _logger.LogDebug(
                "Propagating correlation context to {Method} {Uri} with correlation ID: {CorrelationId}",
                request.Method,
                request.RequestUri,
                _correlationService.CorrelationId);

            try
            {
                var response = await base.SendAsync(request, cancellationToken);

                // Log response correlation
                if (response.Headers.TryGetValues("X-Correlation-ID", out var responseCorrelationIds))
                {
                    _logger.LogDebug(
                        "Received response with correlation ID: {ResponseCorrelationId} for request {CorrelationId}",
                        string.Join(", ", responseCorrelationIds),
                        _correlationService.CorrelationId);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error during request with correlation ID: {CorrelationId}",
                    _correlationService.CorrelationId);
                throw;
            }
        }
    }
}