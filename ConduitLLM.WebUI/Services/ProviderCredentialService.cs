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
    /// Service for managing provider credentials using direct database access
    /// </summary>
    public class ProviderCredentialService : Interfaces.IProviderCredentialService
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<ProviderCredentialService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderCredentialService"/> class.
        /// </summary>
        /// <param name="dbContextFactory">The database context factory.</param>
        /// <param name="logger">The logger.</param>
        public ProviderCredentialService(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<ProviderCredentialService> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderCredentialDto>> GetAllAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var credentials = await context.ProviderCredentials.ToListAsync();
            return credentials.Select(MapToDto);
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderCredentialDto?> GetByIdAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var credential = await context.ProviderCredentials.FindAsync(id);
            return credential != null ? MapToDto(credential) : null;
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderCredentialDto?> GetByProviderNameAsync(string providerName)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var credential = await context.ProviderCredentials
                .FirstOrDefaultAsync(c => c.ProviderName == providerName);
            return credential != null ? MapToDto(credential) : null;
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderCredentialDto?> CreateAsync(ConfigDTOs.CreateProviderCredentialDto credential)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                
                // Check if provider already exists
                var existing = await context.ProviderCredentials
                    .FirstOrDefaultAsync(c => c.ProviderName == credential.ProviderName);
                
                if (existing != null)
                {
                    _logger.LogWarning("Provider credential already exists for provider: {ProviderName}", credential.ProviderName);
                    return null;
                }

                var entity = new ProviderCredential
                {
                    ProviderName = credential.ProviderName,
                    ApiKey = credential.ApiKey,
                    BaseUrl = credential.ApiBase,
                    IsEnabled = credential.IsEnabled
                };

                context.ProviderCredentials.Add(entity);
                await context.SaveChangesAsync();

                return MapToDto(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider credential");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderCredentialDto?> UpdateAsync(int id, ConfigDTOs.UpdateProviderCredentialDto credential)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var entity = await context.ProviderCredentials.FindAsync(id);

                if (entity == null)
                {
                    return null;
                }

                // Update properties
                if (credential.ApiKey == "[REMOVE]")
                {
                    entity.ApiKey = null;
                }
                else if (!string.IsNullOrEmpty(credential.ApiKey))
                {
                    entity.ApiKey = credential.ApiKey;
                }

                entity.BaseUrl = credential.ApiBase ?? entity.BaseUrl;
                entity.IsEnabled = credential.IsEnabled;

                await context.SaveChangesAsync();

                return MapToDto(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider credential");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var entity = await context.ProviderCredentials.FindAsync(id);

                if (entity == null)
                {
                    return false;
                }

                context.ProviderCredentials.Remove(entity);
                await context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider credential");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderConnectionTestResultDto?> TestConnectionAsync(string providerName)
        {
            // This might need to coordinate with the Provider layer to actually test the connection
            // For now, return a basic result
            return await Task.FromResult(new ConfigDTOs.ProviderConnectionTestResultDto
            {
                Success = false,
                Message = "Connection testing not implemented in direct database mode",
                ErrorDetails = null,
                ProviderName = providerName,
                Timestamp = DateTime.UtcNow
            });
        }

        private static ConfigDTOs.ProviderCredentialDto MapToDto(ProviderCredential entity)
        {
            return new ConfigDTOs.ProviderCredentialDto
            {
                Id = entity.Id,
                ProviderName = entity.ProviderName,
                ApiKey = entity.ApiKey ?? string.Empty,
                ApiBase = entity.BaseUrl ?? string.Empty,
                IsEnabled = entity.IsEnabled,
                // These properties don't exist in the entity, so set to null or empty
                Organization = null,
                ProjectId = null,
                Region = null,
                DeploymentName = null,
                ModelEndpoint = null,
                AdditionalConfig = null,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}