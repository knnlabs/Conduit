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

using static ConduitLLM.Configuration.Utilities.LogSanitizer;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for global settings using Entity Framework Core
    /// </summary>
    public class GlobalSettingRepository : IGlobalSettingRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<GlobalSettingRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public GlobalSettingRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<GlobalSettingRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<GlobalSetting?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.GlobalSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(gs => gs.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting global setting with ID {SettingId}", S(id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<GlobalSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.GlobalSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(gs => gs.Key == key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting global setting with key {SettingKey}", S(key));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<GlobalSetting>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.GlobalSettings
                    .AsNoTracking()
                    .OrderBy(gs => gs.Key)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all global settings");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(GlobalSetting globalSetting, CancellationToken cancellationToken = default)
        {
            if (globalSetting == null)
            {
                throw new ArgumentNullException(nameof(globalSetting));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set timestamps
                if (globalSetting.CreatedAt == default)
                {
                    globalSetting.CreatedAt = DateTime.UtcNow;
                }

                globalSetting.UpdatedAt = DateTime.UtcNow;

                dbContext.GlobalSettings.Add(globalSetting);
                await dbContext.SaveChangesAsync(cancellationToken);
                return globalSetting.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating global setting with key '{SettingKey}'",
                    S(globalSetting.Key));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating global setting with key '{SettingKey}'",
                    S(globalSetting.Key));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(GlobalSetting globalSetting, CancellationToken cancellationToken = default)
        {
            if (globalSetting == null)
            {
                throw new ArgumentNullException(nameof(globalSetting));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Ensure the entity is tracked
                dbContext.GlobalSettings.Update(globalSetting);

                // Set the updated timestamp
                globalSetting.UpdatedAt = DateTime.UtcNow;

                // Save changes
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating global setting with ID {SettingId}",
                    S(globalSetting.Id));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating global setting with ID {SettingId}",
                    S(globalSetting.Id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpsertAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Try to find existing setting
                var existingSetting = await dbContext.GlobalSettings
                    .FirstOrDefaultAsync(gs => gs.Key == key, cancellationToken);

                if (existingSetting == null)
                {
                    // Create new setting
                    var newSetting = new GlobalSetting
                    {
                        Key = key,
                        Value = value,
                        Description = description,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    dbContext.GlobalSettings.Add(newSetting);
                }
                else
                {
                    // Update existing setting
                    existingSetting.Value = value;
                    existingSetting.UpdatedAt = DateTime.UtcNow;

                    // Only update description if provided
                    if (description != null)
                    {
                        existingSetting.Description = description;
                    }
                }

                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting global setting with key '{SettingKey}'", S(key));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var globalSetting = await dbContext.GlobalSettings.FindAsync(new object[] { id }, cancellationToken);

                if (globalSetting == null)
                {
                    return false;
                }

                dbContext.GlobalSettings.Remove(globalSetting);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting global setting with ID {SettingId}", S(id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var globalSetting = await dbContext.GlobalSettings
                    .FirstOrDefaultAsync(gs => gs.Key == key, cancellationToken);

                if (globalSetting == null)
                {
                    return false;
                }

                dbContext.GlobalSettings.Remove(globalSetting);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting global setting with key {SettingKey}", S(key));
                throw;
            }
        }
    }
}
