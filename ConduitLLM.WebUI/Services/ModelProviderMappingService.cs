using ConduitLLM.Configuration.Data;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for managing model provider mappings using direct database access
    /// </summary>
    public class ModelProviderMappingService : Interfaces.IModelProviderMappingService
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<ModelProviderMappingService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelProviderMappingService"/> class.
        /// </summary>
        /// <param name="dbContextFactory">The database context factory.</param>
        /// <param name="logger">The logger.</param>
        public ModelProviderMappingService(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<ModelProviderMappingService> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ModelProviderMappingDto>> GetAllAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var mappings = await context.ModelProviderMappings
                .Include(m => m.ProviderCredential)
                .ToListAsync();

            return mappings.Select(MapToDto);
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> GetByIdAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var mapping = await context.ModelProviderMappings
                .Include(m => m.ProviderCredential)
                .FirstOrDefaultAsync(m => m.Id == id);

            return mapping != null ? MapToDto(mapping) : null;
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> GetByModelIdAsync(string modelId)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var mapping = await context.ModelProviderMappings
                .Include(m => m.ProviderCredential)
                .FirstOrDefaultAsync(m => m.ModelAlias == modelId);

            return mapping != null ? MapToDto(mapping) : null;
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> CreateAsync(ConfigDTOs.ModelProviderMappingDto mapping)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                
                var entity = new ConduitLLM.Configuration.Entities.ModelProviderMapping
                {
                    ModelAlias = mapping.ModelId,
                    ProviderModelName = mapping.ProviderModelId,
                    ProviderCredentialId = int.Parse(mapping.ProviderId),
                    MaxContextTokens = mapping.MaxContextLength,
                    IsEnabled = mapping.IsEnabled
                };

                context.ModelProviderMappings.Add(entity);
                await context.SaveChangesAsync();

                // Reload with includes
                var created = await context.ModelProviderMappings
                    .Include(m => m.ProviderCredential)
                    .FirstOrDefaultAsync(m => m.Id == entity.Id);

                return created != null ? MapToDto(created) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> UpdateAsync(ConfigDTOs.ModelProviderMappingDto mapping)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                
                var entity = await context.ModelProviderMappings
                    .FirstOrDefaultAsync(m => m.Id == mapping.Id);

                if (entity == null)
                {
                    return null;
                }

                entity.ModelAlias = mapping.ModelId;
                entity.ProviderModelName = mapping.ProviderModelId;
                entity.ProviderCredentialId = int.Parse(mapping.ProviderId);
                entity.MaxContextTokens = mapping.MaxContextLength;
                entity.IsEnabled = mapping.IsEnabled;
                entity.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();

                // Reload with includes
                var updated = await context.ModelProviderMappings
                    .Include(m => m.ProviderCredential)
                    .FirstOrDefaultAsync(m => m.Id == entity.Id);

                return updated != null ? MapToDto(updated) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                
                var entity = await context.ModelProviderMappings
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (entity == null)
                {
                    return false;
                }

                context.ModelProviderMappings.Remove(entity);
                await context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model provider mapping");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderDataDto>> GetProvidersAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var providers = await context.ProviderCredentials.ToListAsync();

            return providers.Select(p => new ConfigDTOs.ProviderDataDto
            {
                Id = p.Id,
                ProviderName = p.ProviderName
            });
        }

        private static ConfigDTOs.ModelProviderMappingDto MapToDto(Configuration.Entities.ModelProviderMapping entity)
        {
            return new ConfigDTOs.ModelProviderMappingDto
            {
                Id = entity.Id,
                ModelId = entity.ModelAlias,
                ProviderModelId = entity.ProviderModelName,
                ProviderId = entity.ProviderCredentialId.ToString(),
                MaxContextLength = entity.MaxContextTokens,
                IsEnabled = entity.IsEnabled,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}