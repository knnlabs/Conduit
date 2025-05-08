using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Data.Interfaces
{
    /// <summary>
    /// Defines operations for initializing and verifying database connections.
    /// </summary>
    public interface IDatabaseInitializer
    {
        /// <summary>
        /// Ensures the database exists and is ready to use.
        /// </summary>
        /// <param name="ensureCreated">Whether to ensure the database is created if it doesn't exist.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the database cannot be initialized.</exception>
        /// <example>
        /// ```csharp
        /// public async Task InitializeDatabase(IServiceProvider serviceProvider)
        /// {
        ///     var initializer = serviceProvider.GetRequiredService<IDatabaseInitializer>();
        ///     
        ///     try
        ///     {
        ///         // Check if we should create the database if it doesn't exist
        ///         bool ensureCreated = true; 
        ///         if (bool.TryParse(Environment.GetEnvironmentVariable("CONDUIT_DATABASE_ENSURE_CREATED"), out var parsed))
        ///         {
        ///             ensureCreated = parsed;
        ///         }
        ///         
        ///         await initializer.EnsureDatabaseAsync(ensureCreated);
        ///         _logger.LogInformation("Database initialized successfully");
        ///     }
        ///     catch (Exception ex)
        ///     {
        ///         _logger.LogError(ex, "Failed to initialize database");
        ///         throw;
        ///     }
        /// }
        /// ```
        /// </example>
        Task EnsureDatabaseAsync(bool ensureCreated = true, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if the database connection is valid.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>True if the connection is valid, false otherwise.</returns>
        /// <example>
        /// ```csharp
        /// public async Task<bool> CheckDatabaseHealthAsync(IServiceProvider serviceProvider)
        /// {
        ///     var initializer = serviceProvider.GetRequiredService<IDatabaseInitializer>();
        ///     
        ///     try
        ///     {
        ///         bool isHealthy = await initializer.VerifyConnectionAsync();
        ///         if (isHealthy)
        ///         {
        ///             _logger.LogInformation("Database connection verified successfully");
        ///         }
        ///         else
        ///         {
        ///             _logger.LogWarning("Database connection failed verification check");
        ///         }
        ///         
        ///         return isHealthy;
        ///     }
        ///     catch (Exception ex)
        ///     {
        ///         _logger.LogError(ex, "Error verifying database connection");
        ///         return false;
        ///     }
        /// }
        /// ```
        /// </example>
        Task<bool> VerifyConnectionAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Applies any pending migrations to the database.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when migrations cannot be applied.</exception>
        /// <example>
        /// ```csharp
        /// public async Task MigrateDatabase(IServiceProvider serviceProvider)
        /// {
        ///     var initializer = serviceProvider.GetRequiredService<IDatabaseInitializer>();
        ///     
        ///     try
        ///     {
        ///         await initializer.MigrateAsync();
        ///         _logger.LogInformation("Database migrations applied successfully");
        ///     }
        ///     catch (Exception ex)
        ///     {
        ///         _logger.LogError(ex, "Failed to apply database migrations");
        ///         throw;
        ///     }
        /// }
        /// ```
        /// </example>
        Task MigrateAsync(CancellationToken cancellationToken = default);
    }
}