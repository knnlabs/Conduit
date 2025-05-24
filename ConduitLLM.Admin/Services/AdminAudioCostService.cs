using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing audio cost configurations.
    /// </summary>
    public class AdminAudioCostService : IAdminAudioCostService
    {
        private readonly IAudioCostRepository _repository;
        private readonly ILogger<AdminAudioCostService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminAudioCostService"/> class.
        /// </summary>
        public AdminAudioCostService(
            IAudioCostRepository repository,
            ILogger<AdminAudioCostService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<AudioCostDto>> GetAllAsync()
        {
            var costs = await _repository.GetAllAsync();
            return costs.Select(MapToDto).ToList();
        }

        /// <inheritdoc/>
        public async Task<AudioCostDto?> GetByIdAsync(int id)
        {
            var cost = await _repository.GetByIdAsync(id);
            return cost != null ? MapToDto(cost) : null;
        }

        /// <inheritdoc/>
        public async Task<List<AudioCostDto>> GetByProviderAsync(string provider)
        {
            var costs = await _repository.GetByProviderAsync(provider);
            return costs.Select(MapToDto).ToList();
        }

        /// <inheritdoc/>
        public async Task<AudioCostDto?> GetCurrentCostAsync(string provider, string operationType, string? model = null)
        {
            var cost = await _repository.GetCurrentCostAsync(provider, operationType, model);
            return cost != null ? MapToDto(cost) : null;
        }

        /// <inheritdoc/>
        public async Task<List<AudioCostDto>> GetCostHistoryAsync(string provider, string operationType, string? model = null)
        {
            var costs = await _repository.GetCostHistoryAsync(provider, operationType, model);
            return costs.Select(MapToDto).ToList();
        }

        /// <inheritdoc/>
        public async Task<AudioCostDto> CreateAsync(CreateAudioCostDto dto)
        {
            var cost = new AudioCost
            {
                Provider = dto.Provider,
                OperationType = dto.OperationType,
                Model = dto.Model,
                CostUnit = dto.CostUnit,
                CostPerUnit = dto.CostPerUnit,
                MinimumCharge = dto.MinimumCharge,
                AdditionalFactors = dto.AdditionalFactors,
                IsActive = dto.IsActive,
                EffectiveFrom = dto.EffectiveFrom,
                EffectiveTo = dto.EffectiveTo
            };

            var created = await _repository.CreateAsync(cost);
            _logger.LogInformation("Created audio cost configuration {Id} for {Provider} {Operation}", 
                created.Id, created.Provider, created.OperationType);

            return MapToDto(created);
        }

        /// <inheritdoc/>
        public async Task<AudioCostDto?> UpdateAsync(int id, UpdateAudioCostDto dto)
        {
            var cost = await _repository.GetByIdAsync(id);
            if (cost == null)
            {
                return null;
            }

            // Update properties
            cost.Provider = dto.Provider;
            cost.OperationType = dto.OperationType;
            cost.Model = dto.Model;
            cost.CostUnit = dto.CostUnit;
            cost.CostPerUnit = dto.CostPerUnit;
            cost.MinimumCharge = dto.MinimumCharge;
            cost.AdditionalFactors = dto.AdditionalFactors;
            cost.IsActive = dto.IsActive;
            cost.EffectiveFrom = dto.EffectiveFrom;
            cost.EffectiveTo = dto.EffectiveTo;

            var updated = await _repository.UpdateAsync(cost);
            _logger.LogInformation("Updated audio cost configuration {Id}", id);

            return MapToDto(updated);
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteAsync(id);
            if (deleted)
            {
                _logger.LogInformation("Deleted audio cost configuration {Id}", id);
            }
            return deleted;
        }

        /// <inheritdoc/>
        public async Task<BulkImportResult> ImportCostsAsync(string data, string format)
        {
            // TODO: Implement bulk import functionality
            _logger.LogWarning("Bulk import not yet implemented");
            return await Task.FromResult(new BulkImportResult
            {
                SuccessCount = 0,
                FailureCount = 0,
                Errors = new List<string> { "Bulk import not yet implemented" }
            });
        }

        /// <inheritdoc/>
        public async Task<string> ExportCostsAsync(string format, string? provider = null)
        {
            // TODO: Implement export functionality
            _logger.LogWarning("Export not yet implemented");
            return await Task.FromResult("Export not yet implemented");
        }

        private static AudioCostDto MapToDto(AudioCost cost)
        {
            return new AudioCostDto
            {
                Id = cost.Id,
                Provider = cost.Provider,
                OperationType = cost.OperationType,
                Model = cost.Model,
                CostUnit = cost.CostUnit,
                CostPerUnit = cost.CostPerUnit,
                MinimumCharge = cost.MinimumCharge,
                AdditionalFactors = cost.AdditionalFactors,
                IsActive = cost.IsActive,
                EffectiveFrom = cost.EffectiveFrom,
                EffectiveTo = cost.EffectiveTo,
                CreatedAt = cost.CreatedAt,
                UpdatedAt = cost.UpdatedAt
            };
        }
    }
}