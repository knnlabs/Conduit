using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Data.Constants;
using ConduitLLM.Core.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace ConduitLLM.Core.Data
{
    /// <summary>
    /// Provides database initialization and verification for Entity Framework Core.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    public class DatabaseInitializer<TContext> : IDatabaseInitializer where TContext : DbContext
    {
        private readonly IDbContextFactory<TContext> _contextFactory;
        private readonly IConnectionStringManager _connectionStringManager;
        private readonly ILogger<DatabaseInitializer<TContext>> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseInitializer{TContext}"/> class.
        /// </summary>
        /// <param name="contextFactory">The database context factory.</param>
        /// <param name="connectionStringManager">The connection string manager.</param>
        /// <param name="logger">The logger.</param>
        public DatabaseInitializer(
            IDbContextFactory<TContext> contextFactory,
            IConnectionStringManager connectionStringManager,
            ILogger<DatabaseInitializer<TContext>> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _connectionStringManager = connectionStringManager ?? throw new ArgumentNullException(nameof(connectionStringManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Create a retry policy for database operations
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: DatabaseConstants.MAX_RETRY_COUNT,
                    sleepDurationProvider: retryAttempt => 
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * DatabaseConstants.RETRY_CONNECTION_TIMEOUT_SECONDS),
                    onRetry: (exception, timeSpan, attempt, context) =>
                    {
                        _logger.LogWarning(
                            exception,
                            "Database operation attempt {Attempt} failed with error: {ErrorMessage}. Retrying in {RetryTimespan}...",
                            attempt,
                            exception.Message,
                            timeSpan);
                    });
        }

        /// <inheritdoc/>
        public async Task EnsureDatabaseAsync(bool ensureCreated = true, CancellationToken cancellationToken = default)
        {
            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                    
                    if (ensureCreated)
                    {
                        _logger.LogInformation("Ensuring database exists");
                        await context.Database.EnsureCreatedAsync(cancellationToken);
                        _logger.LogInformation("Database exists");
                    }
                    else
                    {
                        _logger.LogInformation("Verifying database connection");
                        await context.Database.CanConnectAsync(cancellationToken);
                        _logger.LogInformation("Database connection verified");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure database exists");
                throw new InvalidOperationException("Failed to initialize database. See inner exception for details.", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> VerifyConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                    return await context.Database.CanConnectAsync(cancellationToken);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify database connection");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                    
                    _logger.LogInformation("Applying database migrations");
                    await context.Database.MigrateAsync(cancellationToken);
                    _logger.LogInformation("Database migrations applied successfully");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply database migrations");
                throw new InvalidOperationException("Failed to apply database migrations. See inner exception for details.", ex);
            }
        }
    }
}