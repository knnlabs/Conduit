using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Configuration.Utilities.LogSanitizer;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for model provider mappings using Entity Framework Core.
    /// </summary>
    public class ModelProviderMappingRepository : IModelProviderMappingRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<ModelProviderMappingRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public ModelProviderMappingRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<ModelProviderMappingRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<ConduitLLM.Configuration.Entities.ModelProviderMapping?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .Include(m => m.ProviderCredential)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping with ID {MappingId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ConduitLLM.Configuration.Entities.ModelProviderMapping?> GetByModelNameAsync(
            string modelName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .Include(m => m.ProviderCredential)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ModelAlias == modelName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping for model {ModelName}", modelName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ConduitLLM.Configuration.Entities.ModelProviderMapping>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .Include(m => m.ProviderCredential)
                    .AsNoTracking()
                    .OrderBy(m => m.ModelAlias)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model provider mappings");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ConduitLLM.Configuration.Entities.ModelProviderMapping>> GetByProviderAsync(
            string providerName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // First find the provider credential
                var credential = await dbContext.ProviderCredentials
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pc => pc.ProviderName == providerName, cancellationToken);

                if (credential == null)
                {
                    return new List<ConduitLLM.Configuration.Entities.ModelProviderMapping>();
                }

                // Then find mappings with this credential ID
                return await dbContext.ModelProviderMappings
                    .Include(m => m.ProviderCredential)
                    .AsNoTracking()
                    .Where(m => m.ProviderCredentialId == credential.Id)
                    .OrderBy(m => m.ModelAlias)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mappings for provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(
            ConduitLLM.Configuration.Entities.ModelProviderMapping modelProviderMapping,
            CancellationToken cancellationToken = default)
        {
            if (modelProviderMapping == null)
            {
                throw new ArgumentNullException(nameof(modelProviderMapping));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set timestamps
                modelProviderMapping.CreatedAt = DateTime.UtcNow;
                modelProviderMapping.UpdatedAt = DateTime.UtcNow;

                dbContext.ModelProviderMappings.Add(modelProviderMapping);
                await dbContext.SaveChangesAsync(cancellationToken);

                return modelProviderMapping.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping for {ModelAlias}", modelProviderMapping.ModelAlias.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(
            ConduitLLM.Configuration.Entities.ModelProviderMapping modelProviderMapping,
            CancellationToken cancellationToken = default)
        {
            if (modelProviderMapping == null)
            {
                throw new ArgumentNullException(nameof(modelProviderMapping));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Get existing entity to ensure it exists
                var existingEntity = await dbContext.ModelProviderMappings
                    .FirstOrDefaultAsync(m => m.Id == modelProviderMapping.Id, cancellationToken);

                if (existingEntity == null)
                {
                    _logger.LogWarning("Cannot update non-existent model provider mapping with ID {MappingId}", modelProviderMapping.Id);
                    return false;
                }

                // Update fields
                existingEntity.ModelAlias = modelProviderMapping.ModelAlias;
                existingEntity.ProviderModelName = modelProviderMapping.ProviderModelName;
                existingEntity.ProviderCredentialId = modelProviderMapping.ProviderCredentialId;
                existingEntity.IsEnabled = modelProviderMapping.IsEnabled;
                existingEntity.MaxContextTokens = modelProviderMapping.MaxContextTokens;
                
                // Update capability fields
                existingEntity.SupportsVision = modelProviderMapping.SupportsVision;
                existingEntity.SupportsAudioTranscription = modelProviderMapping.SupportsAudioTranscription;
                existingEntity.SupportsTextToSpeech = modelProviderMapping.SupportsTextToSpeech;
                existingEntity.SupportsRealtimeAudio = modelProviderMapping.SupportsRealtimeAudio;
                existingEntity.SupportsImageGeneration = modelProviderMapping.SupportsImageGeneration;
                existingEntity.TokenizerType = modelProviderMapping.TokenizerType;
                existingEntity.SupportedVoices = modelProviderMapping.SupportedVoices;
                existingEntity.SupportedLanguages = modelProviderMapping.SupportedLanguages;
                existingEntity.SupportedFormats = modelProviderMapping.SupportedFormats;
                existingEntity.IsDefault = modelProviderMapping.IsDefault;
                existingEntity.DefaultCapabilityType = modelProviderMapping.DefaultCapabilityType;
                
                existingEntity.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Updating model mapping {ModelAlias}: ImageGen={ImageGen}, Vision={Vision}, TTS={TTS}, Audio={Audio}, Realtime={Realtime}",
                    existingEntity.ModelAlias,
                    existingEntity.SupportsImageGeneration,
                    existingEntity.SupportsVision,
                    existingEntity.SupportsTextToSpeech,
                    existingEntity.SupportsAudioTranscription,
                    existingEntity.SupportsRealtimeAudio);

                await dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping with ID {MappingId}", modelProviderMapping.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var entity = await dbContext.ModelProviderMappings
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

                if (entity == null)
                {
                    _logger.LogWarning("Cannot delete non-existent model provider mapping with ID {MappingId}", id);
                    return false;
                }

                dbContext.ModelProviderMappings.Remove(entity);
                await dbContext.SaveChangesAsync(cancellationToken);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model provider mapping with ID {MappingId}", id);
                throw;
            }
        }
    }
}
