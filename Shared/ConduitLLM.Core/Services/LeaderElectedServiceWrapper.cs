using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Wraps an existing IHostedService with leader election capability.
    /// Ensures the inner service only runs on the leader instance.
    /// </summary>
    public class LeaderElectedServiceWrapper : LeaderElectedBackgroundService
    {
        private readonly IHostedService _innerService;
        private readonly ILogger<LeaderElectedServiceWrapper> _logger;
        private CancellationTokenSource? _innerServiceCts;
        private readonly SemaphoreSlim _innerServiceLock = new(1, 1);
        private bool _wrapperDisposed;

        public LeaderElectedServiceWrapper(
            IHostedService innerService,
            ILeaderElectionService leaderElectionService,
            ILogger<LeaderElectedServiceWrapper> logger,
            string serviceName)
            : base(leaderElectionService, logger, serviceName)
        {
            _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task OnBecameLeaderAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting inner service {ServiceName} as leader", ServiceName);

            await _innerServiceLock.WaitAsync(cancellationToken);
            try
            {
                // Create a new cancellation token source for the inner service
                _innerServiceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Start the inner service
                await _innerService.StartAsync(_innerServiceCts.Token);
            }
            finally
            {
                _innerServiceLock.Release();
            }
        }

        protected override async Task OnLostLeadershipAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping inner service {ServiceName} as leadership was lost", ServiceName);

            await StopInnerServiceAsync();
        }

        protected override async Task ExecuteLeaderWorkAsync(CancellationToken cancellationToken)
        {
            // The inner service is already running in its own execution context
            // Just wait for a short period before checking leadership again
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }

        private async Task StopInnerServiceAsync()
        {
            await _innerServiceLock.WaitAsync();
            try
            {
                if (_innerServiceCts != null)
                {
                    await _innerServiceCts.CancelAsync();

                    try
                    {
                        // Give the service time to stop gracefully
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        await _innerService.StopAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during cancellation
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error stopping inner service {ServiceName}", ServiceName);
                    }
                    finally
                    {
                        _innerServiceCts.Dispose();
                        _innerServiceCts = null;
                    }
                }
            }
            finally
            {
                _innerServiceLock.Release();
            }
        }

        /// <summary>
        /// Asynchronously disposes the wrapper and inner service
        /// </summary>
        public override async ValueTask DisposeAsync()
        {
            if (_wrapperDisposed) return;
            _wrapperDisposed = true;

            // Stop the inner service if it's running
            await StopInnerServiceAsync();

            // Dispose the inner service if it supports async disposal
            if (_innerService is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (_innerService is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _innerServiceLock.Dispose();

            await base.DisposeAsync();
        }

        /// <summary>
        /// Synchronously disposes the wrapper and inner service.
        /// Prefer using DisposeAsync when possible.
        /// </summary>
        public override void Dispose()
        {
            if (_wrapperDisposed) return;
            _wrapperDisposed = true;

            // Best-effort synchronous cleanup
            _innerServiceLock.Wait();
            try
            {
                if (_innerServiceCts != null)
                {
                    _innerServiceCts.Cancel();

                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        _innerService.StopAsync(cts.Token).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error stopping inner service during disposal");
                    }
                    finally
                    {
                        _innerServiceCts.Dispose();
                        _innerServiceCts = null;
                    }
                }
            }
            finally
            {
                _innerServiceLock.Release();
            }

            // Dispose the inner service if it's disposable
            if (_innerService is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _innerServiceLock.Dispose();

            base.Dispose();
        }
    }
}
