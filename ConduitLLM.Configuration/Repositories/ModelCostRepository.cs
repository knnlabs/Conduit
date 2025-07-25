using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
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
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
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
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost with ID {CostId}", LogSanitizer.SanitizeObject(id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ModelCost?> GetByModelNameAsync(string modelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelCosts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ModelIdPattern == modelName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost for model {ModelName}", LogSanitizer.SanitizeObject(modelName));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ModelCost?> GetByModelIdPatternAsync(string modelIdPattern, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelIdPattern))
            {
                throw new ArgumentException("Model ID pattern cannot be null or empty", nameof(modelIdPattern));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelCosts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ModelIdPattern == modelIdPattern, cancellationToken);
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error getting model cost for model ID pattern {ModelIdPattern}", modelIdPattern.Replace(Environment.NewLine, ""));
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
                    .OrderBy(m => m.ModelIdPattern)
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
        public async Task<List<ModelCost>> GetByProviderAsync(string providerName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Parse provider name to ProviderType
                if (!Enum.TryParse<ProviderType>(providerName, true, out var providerType))
                {
                    _logger.LogWarning("Invalid provider name: {ProviderName}", providerName);
                    return new List<ModelCost>();
                }

                // First, get provider credentials for this provider
                var providerCredential = await dbContext.ProviderCredentials
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pc => pc.ProviderType == providerType, cancellationToken);

                if (providerCredential == null)
                {
_logger.LogWarning("No provider credential found for provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                    return new List<ModelCost>();
                }

                // Get all model mappings for this provider where provider credential matches
                var providerMappings = await dbContext.ModelProviderMappings
                    .AsNoTracking()
                    .Where(m => m.ProviderCredentialId == providerCredential.Id)
                    .ToListAsync(cancellationToken);

                if (!providerMappings.Any())
                {
_logger.LogInformation("No model mappings found for provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                    return new List<ModelCost>();
                }

                // Get the list of model patterns used by this provider
                var allModelPatterns = new List<string>();
                // Extract provider model names from mappings for pattern matching
                var exactModelNames = providerMappings.Select(m => m.ProviderModelName).ToList();

                // Get all model costs
                var allModelCosts = await dbContext.ModelCosts
                    .AsNoTracking()
                    .OrderBy(m => m.ModelIdPattern)
                    .ToListAsync(cancellationToken);

                // Filter model costs that match:
                // 1. Exact matches to provider model names
                // 2. Wildcard patterns that match provider model names
                var result = new List<ModelCost>();

                // Add exact matches
                result.AddRange(allModelCosts.Where(c => exactModelNames.Contains(c.ModelIdPattern)));

                // Add wildcard matches
                var wildcardPatterns = allModelCosts.Where(c => c.ModelIdPattern.Contains('*')).ToList();
                foreach (var pattern in wildcardPatterns)
                {
                    string patternPrefix = pattern.ModelIdPattern.TrimEnd('*');

                    // Add if any provider model starts with this pattern prefix
                    if (exactModelNames.Any(modelName => modelName.StartsWith(patternPrefix)))
                    {
                        result.Add(pattern);
                    }
                }

                // Also include model patterns that have the provider name in them
                var providerPrefixPatterns = allModelCosts
                    .Where(c => c.ModelIdPattern.StartsWith($"{providerName}/") ||
                                c.ModelIdPattern.StartsWith($"{providerName}-") ||
                                c.ModelIdPattern.StartsWith(providerName.ToLowerInvariant()))
                    .ToList();

                foreach (var pattern in providerPrefixPatterns)
                {
                    if (!result.Any(r => r.Id == pattern.Id))
                    {
                        result.Add(pattern);
                    }
                }

                return result.OrderBy(m => m.ModelIdPattern).ToList();
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error getting model costs for provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelCost>> GetByProviderAsync(ProviderType providerType, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // First, get provider credentials for this provider
                var providerCredential = await dbContext.ProviderCredentials
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pc => pc.ProviderType == providerType, cancellationToken);

                if (providerCredential == null)
                {
                    _logger.LogWarning("No provider credential found for provider type {ProviderType}", providerType);
                    return new List<ModelCost>();
                }

                // Get all model mappings for this provider where provider credential matches
                var providerMappings = await dbContext.ModelProviderMappings
                    .AsNoTracking()
                    .Where(m => m.ProviderCredentialId == providerCredential.Id)
                    .ToListAsync(cancellationToken);

                if (!providerMappings.Any())
                {
                    _logger.LogInformation("No model mappings found for provider type {ProviderType}", providerType);
                    return new List<ModelCost>();
                }

                // Get the list of model patterns used by this provider
                var allModelPatterns = new List<string>();
                // Extract provider model names from mappings for pattern matching
                var exactModelNames = providerMappings.Select(m => m.ProviderModelName).ToList();

                // Get all model costs
                var allModelCosts = await dbContext.ModelCosts
                    .AsNoTracking()
                    .OrderBy(m => m.ModelIdPattern)
                    .ToListAsync(cancellationToken);

                // Filter model costs that match:
                // 1. Exact matches to provider model names
                // 2. Wildcard patterns that match provider model names
                var result = new List<ModelCost>();

                // Add exact matches
                result.AddRange(allModelCosts.Where(c => exactModelNames.Contains(c.ModelIdPattern)));

                // Add wildcard matches
                var wildcardPatterns = allModelCosts.Where(c => c.ModelIdPattern.Contains('*')).ToList();
                foreach (var pattern in wildcardPatterns)
                {
                    string patternPrefix = pattern.ModelIdPattern.TrimEnd('*');

                    // Add if any provider model starts with this pattern prefix
                    if (exactModelNames.Any(modelName => modelName.StartsWith(patternPrefix)))
                    {
                        result.Add(pattern);
                    }
                }

                // Also include model patterns that have the provider name in them
                string providerName = providerType.ToString();
                var providerPrefixPatterns = allModelCosts
                    .Where(c => c.ModelIdPattern.StartsWith($"{providerName}/") ||
                                c.ModelIdPattern.StartsWith($"{providerName}-") ||
                                c.ModelIdPattern.StartsWith(providerName.ToLowerInvariant()))
                    .ToList();

                foreach (var pattern in providerPrefixPatterns)
                {
                    if (!result.Any(r => r.Id == pattern.Id))
                    {
                        result.Add(pattern);
                    }
                }

                return result.OrderBy(m => m.ModelIdPattern).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model costs for provider type {ProviderType}", providerType);
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
                    _logger.LogError(ex, "Transaction rolled back while creating model cost for model '{ModelIdPattern}'",
                        LogSanitizer.SanitizeObject(modelCost.ModelIdPattern.Replace(Environment.NewLine, "")));
                    throw;
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating model cost for model '{ModelIdPattern}'",
                    LogSanitizer.SanitizeObject(modelCost.ModelIdPattern.Replace(Environment.NewLine, "")));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model cost for model '{ModelIdPattern}'",
                    LogSanitizer.SanitizeObject(modelCost.ModelIdPattern.Replace(Environment.NewLine, "")));
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
