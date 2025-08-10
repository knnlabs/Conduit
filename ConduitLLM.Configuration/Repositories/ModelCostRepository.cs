using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Utilities;
using ModelProviderMappingEntity = ConduitLLM.Configuration.Entities.ModelProviderMapping;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for model costs using Entity Framework Core.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This repository provides data access operations for model cost entities using Entity Framework Core.
    /// It implements the <see cref="IModelCostRepository"/> interface and provides concrete implementations
    /// for all required operations.
    /// </para>
    /// <para>
    /// The implementation follows these principles:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Using short-lived DbContext instances for better performance and reliability</description></item>
    ///   <item><description>Comprehensive error handling with detailed logging</description></item>
    ///   <item><description>Optimistic concurrency control for update operations</description></item>
    ///   <item><description>Non-tracking queries for read operations to improve performance</description></item>
    ///   <item><description>Automatic timestamp management for auditing purposes</description></item>
    ///   <item><description>Transaction-based operations for data consistency</description></item>
    /// </list>
    /// <para>
    /// ModelCost entities store pricing information for different LLM models, including input token costs,
    /// output token costs, and additional costs for specific operations like embeddings or image generation.
    /// This repository enables the application to manage these cost records and calculate usage expenses.
    /// </para>
    /// </remarks>
    public class ModelCostRepository : IModelCostRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<ModelCostRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelCostRepository"/> class.
        /// </summary>
        /// <param name="dbContextFactory">The database context factory used to create DbContext instances.</param>
        /// <param name="logger">The logger for recording diagnostic information.</param>
        /// <exception cref="ArgumentNullException">Thrown when dbContextFactory or logger is null.</exception>
        /// <remarks>
        /// This constructor initializes the repository with the required dependencies:
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///       A DbContext factory that creates ConfigurationDbContext instances for data access operations.
        ///       Using a factory pattern allows the repository to create short-lived context instances for
        ///       each operation, which is recommended for web applications.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///       A logger for capturing diagnostic information and errors during repository operations.
        ///       This is especially important for data access operations to help diagnose issues in production.
        ///     </description>
        ///   </item>
        /// </list>
        /// </remarks>
        public ModelCostRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<ModelCostRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<ModelCost?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelCosts
                    .AsNoTracking()
                    .Include(m => m.ModelCostMappings)
                        .ThenInclude(mcm => mcm.ModelProviderMapping)
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost with ID {CostId}", LogSanitizer.SanitizeObject(id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ModelCost?> GetByCostNameAsync(string costName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(costName))
            {
                throw new ArgumentException("Cost name cannot be null or empty", nameof(costName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelCosts
                    .AsNoTracking()
                    .Include(m => m.ModelCostMappings)
                        .ThenInclude(mcm => mcm.ModelProviderMapping)
                    .FirstOrDefaultAsync(m => m.CostName == costName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost with name {CostName}", LogSanitizer.SanitizeObject(costName));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelCost>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelCosts
                    .AsNoTracking()
                    .Include(m => m.ModelCostMappings)
                        .ThenInclude(mcm => mcm.ModelProviderMapping)
                    .OrderBy(m => m.CostName)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model costs");
                throw;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// This implementation retrieves model costs associated with a specific provider by:
        /// </para>
        /// <list type="number">
        ///   <item>
        ///     <description>Finding the provider's credential record by name</description>
        ///   </item>
        ///   <item>
        ///     <description>Retrieving all model mappings associated with that provider</description>
        ///   </item>
        ///   <item>
        ///     <description>Finding cost records that match the provider's model names exactly</description>
        ///   </item>
        ///   <item>
        ///     <description>Finding cost records with wildcard patterns that match the provider's models</description>
        ///   </item>
        ///   <item>
        ///     <description>Finding cost records that have the provider name in their pattern</description>
        ///   </item>
        /// </list>
        /// <para>
        /// This approach ensures that all cost records related to a provider are returned,
        /// even if they use different naming conventions or wildcard patterns.
        /// </para>
        /// </remarks>

        /// <inheritdoc/>
        public async Task<List<ModelCost>> GetByProviderAsync(int providerId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // First, verify provider exists
                var provider = await dbContext.Providers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);

                if (provider == null)
                {
                    _logger.LogWarning("No provider found with ID {ProviderId}", providerId);
                    return new List<ModelCost>();
                }

                // Get all model mappings for this provider
                var providerMappings = await dbContext.ModelProviderMappings
                    .AsNoTracking()
                    .Where(m => m.ProviderId == providerId)
                    .ToListAsync(cancellationToken);

                if (!providerMappings.Any())
                {
                    _logger.LogInformation("No model mappings found for provider {ProviderId}", providerId);
                    return new List<ModelCost>();
                }

                // Get the list of model patterns used by this provider
                var allModelPatterns = new List<string>();
                // Extract provider model names from mappings for pattern matching
                var exactModelNames = providerMappings.Select(m => m.ProviderModelId).ToList();

                // Get all model costs
                // Get all model costs that are associated with models from this provider
                var costs = await dbContext.ModelCosts
                    .AsNoTracking()
                    .Include(m => m.ModelCostMappings)
                        .ThenInclude(mcm => mcm.ModelProviderMapping)
                    .Where(m => m.ModelCostMappings.Any(mcm => 
                        mcm.ModelProviderMapping.ProviderId == providerId && 
                        mcm.IsActive))
                    .OrderBy(m => m.CostName)
                    .ToListAsync(cancellationToken);

                return costs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model costs for provider {ProviderId}", providerId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
        {
            if (modelCost == null)
            {
                throw new ArgumentNullException(nameof(modelCost));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Use a transaction to ensure atomicity
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // Set timestamps
                    modelCost.CreatedAt = DateTime.UtcNow;
                    modelCost.UpdatedAt = DateTime.UtcNow;

                    dbContext.ModelCosts.Add(modelCost);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    // Commit the transaction
                    await transaction.CommitAsync(cancellationToken);

                    return modelCost.Id;
                }
                catch (Exception ex)
                {
                    // Rollback the transaction on error
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Transaction rolled back while creating model cost '{CostName}'",
                        LogSanitizer.SanitizeObject(modelCost.CostName.Replace(Environment.NewLine, "")));
                    throw;
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating model cost '{CostName}'",
                    LogSanitizer.SanitizeObject(modelCost.CostName.Replace(Environment.NewLine, "")));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model cost '{CostName}'",
                    LogSanitizer.SanitizeObject(modelCost.CostName.Replace(Environment.NewLine, "")));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
        {
            if (modelCost == null)
            {
                throw new ArgumentNullException(nameof(modelCost));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Use a transaction to ensure atomicity
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // Set updated timestamp
                    modelCost.UpdatedAt = DateTime.UtcNow;

                    dbContext.ModelCosts.Update(modelCost);
                    int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);

                    // Commit the transaction
                    await transaction.CommitAsync(cancellationToken);

                    return rowsAffected > 0;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    // Rollback the transaction on error
                    await transaction.RollbackAsync(cancellationToken);

                    _logger.LogError(ex, "Concurrency error updating model cost with ID {CostId}", LogSanitizer.SanitizeObject(modelCost.Id));

                    // Handle concurrency issues by reloading and reapplying changes with a new transaction
                    try
                    {
                        using var retryDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                        await using var retryTransaction = await retryDbContext.Database.BeginTransactionAsync(cancellationToken);

                        var existingEntity = await retryDbContext.ModelCosts.FindAsync(new object[] { modelCost.Id }, cancellationToken);

                        if (existingEntity == null)
                        {
                            return false;
                        }

                        // Update properties
                        retryDbContext.Entry(existingEntity).CurrentValues.SetValues(modelCost);
                        existingEntity.UpdatedAt = DateTime.UtcNow;

                        int rowsAffected = await retryDbContext.SaveChangesAsync(cancellationToken);

                        // Commit the retry transaction
                        await retryTransaction.CommitAsync(cancellationToken);

                        return rowsAffected > 0;
                    }
                    catch (Exception retryEx)
                    {
                        _logger.LogError(retryEx, "Error during retry of model cost update with ID {CostId}", LogSanitizer.SanitizeObject(modelCost.Id));
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    // Rollback the transaction on error
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Transaction rolled back while updating model cost with ID {CostId}",
                        LogSanitizer.SanitizeObject(modelCost.Id));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model cost with ID {CostId}",
                    LogSanitizer.SanitizeObject(modelCost.Id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Use a transaction to ensure atomicity
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    var modelCost = await dbContext.ModelCosts.FindAsync(new object[] { id }, cancellationToken);

                    if (modelCost == null)
                    {
                        return false;
                    }

                    dbContext.ModelCosts.Remove(modelCost);
                    int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);

                    // Commit the transaction
                    await transaction.CommitAsync(cancellationToken);

                    return rowsAffected > 0;
                }
                catch (Exception ex)
                {
                    // Rollback the transaction on error
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Transaction rolled back while deleting model cost with ID {CostId}", LogSanitizer.SanitizeObject(id));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model cost with ID {CostId}", LogSanitizer.SanitizeObject(id));
                throw;
            }
        }
    }
}
