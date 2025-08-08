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
    /// Repository implementation for virtual keys using Entity Framework Core.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This repository provides data access operations for virtual key entities using Entity Framework Core.
    /// It implements the <see cref="IVirtualKeyRepository"/> interface and provides concrete implementations
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
    /// </list>
    /// <para>
    /// The repository requires a database factory to create DbContext instances on demand,
    /// ensuring that each operation uses a fresh context with a clean change tracker.
    /// </para>
    /// </remarks>
    public class VirtualKeyRepository : IVirtualKeyRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<VirtualKeyRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualKeyRepository"/> class.
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
        public VirtualKeyRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<VirtualKeyRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<VirtualKey?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.VirtualKeys
                    .AsNoTracking()
                    .Include(vk => vk.VirtualKeyGroup)
                    .FirstOrDefaultAsync(vk => vk.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual key with ID {KeyId}", LogSanitizer.SanitizeObject(id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<VirtualKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(keyHash))
            {
                throw new ArgumentException("Key hash cannot be null or empty", nameof(keyHash));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.VirtualKeys
                    .AsNoTracking()
                    .FirstOrDefaultAsync(vk => vk.KeyHash == keyHash, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual key by hash");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<VirtualKey>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.VirtualKeys
                    .AsNoTracking()
                    .OrderBy(vk => vk.KeyName)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all virtual keys");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(VirtualKey virtualKey, CancellationToken cancellationToken = default)
        {
            if (virtualKey == null)
            {
                throw new ArgumentNullException(nameof(virtualKey));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                dbContext.VirtualKeys.Add(virtualKey);
                await dbContext.SaveChangesAsync(cancellationToken);
                return virtualKey.Id;
            }
            catch (DbUpdateException ex)
            {
_logger.LogError(ex, "Database error creating virtual key '{KeyName}'", virtualKey.KeyName.Replace(Environment.NewLine, ""));
                throw;
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error creating virtual key '{KeyName}'", virtualKey.KeyName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(VirtualKey virtualKey, CancellationToken cancellationToken = default)
        {
            if (virtualKey == null)
            {
                throw new ArgumentNullException(nameof(virtualKey));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Ensure the entity is tracked
                dbContext.VirtualKeys.Update(virtualKey);

                // Set the updated timestamp
                virtualKey.UpdatedAt = DateTime.UtcNow;

                // Save changes
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating virtual key with ID {KeyId}", LogSanitizer.SanitizeObject(virtualKey.Id));

                // Handle concurrency issues by reloading and reapplying changes if needed
                try
                {
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                    var existingEntity = await dbContext.VirtualKeys.FindAsync(new object[] { virtualKey.Id }, cancellationToken);

                    if (existingEntity == null)
                    {
                        return false;
                    }

                    // Update properties
                    dbContext.Entry(existingEntity).CurrentValues.SetValues(virtualKey);
                    existingEntity.UpdatedAt = DateTime.UtcNow;

                    int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                    return rowsAffected > 0;
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Error during retry of virtual key update with ID {KeyId}", LogSanitizer.SanitizeObject(virtualKey.Id));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating virtual key with ID {KeyId}", LogSanitizer.SanitizeObject(virtualKey.Id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var virtualKey = await dbContext.VirtualKeys.FindAsync(new object[] { id }, cancellationToken);

                if (virtualKey == null)
                {
                    return false;
                }

                dbContext.VirtualKeys.Remove(virtualKey);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting virtual key with ID {KeyId}", LogSanitizer.SanitizeObject(id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string keyHash, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var virtualKey = await dbContext.VirtualKeys
                    .Where(vk => vk.KeyHash == keyHash)
                    .FirstOrDefaultAsync(cancellationToken);

                if (virtualKey == null)
                {
                    return false;
                }

                dbContext.VirtualKeys.Remove(virtualKey);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Deleted virtual key with hash {KeyHash}", LogSanitizer.SanitizeObject(keyHash));
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting virtual key with hash {KeyHash}", LogSanitizer.SanitizeObject(keyHash));
                throw;
            }
        }

    }
}
