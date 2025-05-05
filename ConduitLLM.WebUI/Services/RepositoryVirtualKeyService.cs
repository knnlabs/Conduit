using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Adapter class that implements IVirtualKeyService using IVirtualKeyServiceNew
    /// </summary>
    public class RepositoryVirtualKeyService : IVirtualKeyService 
    {
        private readonly IVirtualKeyServiceNew _virtualKeyService;
        private readonly ILogger<RepositoryVirtualKeyService> _logger;

        /// <summary>
        /// Initializes a new instance of the RepositoryVirtualKeyService
        /// </summary>
        public RepositoryVirtualKeyService(
            IVirtualKeyServiceNew virtualKeyService,
            ILogger<RepositoryVirtualKeyService> logger)
        {
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
        {
            return await _virtualKeyService.GenerateVirtualKeyAsync(request);
        }

        /// <inheritdoc />
        public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            return await _virtualKeyService.GetVirtualKeyInfoAsync(id);
        }

        /// <inheritdoc />
        public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
        {
            return await _virtualKeyService.ListVirtualKeysAsync();
        }

        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
        {
            return await _virtualKeyService.UpdateVirtualKeyAsync(id, request);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            return await _virtualKeyService.DeleteVirtualKeyAsync(id);
        }

        /// <inheritdoc />
        public async Task<bool> ResetSpendAsync(int id)
        {
            return await _virtualKeyService.ResetSpendAsync(id);
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            return await _virtualKeyService.ValidateVirtualKeyAsync(key, requestedModel);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
        {
            return await _virtualKeyService.UpdateSpendAsync(keyId, cost);
        }

        /// <inheritdoc />
        public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
        {
            return await _virtualKeyService.ResetBudgetIfExpiredAsync(keyId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            return await _virtualKeyService.GetVirtualKeyInfoForValidationAsync(keyId, cancellationToken);
        }
    }
}