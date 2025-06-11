using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.WebUI.Extensions;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Adapter class that implements Core.Interfaces.IVirtualKeyService using the repository-based virtual key service
    /// </summary>
    /// <remarks>
    /// This adapter class bridges between the core interface (Core.Interfaces.IVirtualKeyService) and
    /// the repository pattern implementation (WebUI.Interfaces.IVirtualKeyService). It enables gradual
    /// migration to the repository pattern while maintaining backward compatibility with components
    /// that depend on the original interface.
    /// 
    /// The adapter forwards all method calls to the underlying repository-based implementation,
    /// providing a seamless transition between the two interface designs. This design pattern allows
    /// for incremental refactoring of the codebase without breaking existing functionality.
    /// </remarks>
    public class RepositoryVirtualKeyService : ConduitLLM.Core.Interfaces.IVirtualKeyService
    {
        private readonly ConduitLLM.WebUI.Interfaces.IVirtualKeyService _virtualKeyService;
        private readonly ILogger<RepositoryVirtualKeyService> _logger;

        /// <summary>
        /// Initializes a new instance of the RepositoryVirtualKeyService adapter
        /// </summary>
        /// <param name="virtualKeyService">The repository-based virtual key service implementation</param>
        /// <param name="logger">The logger instance</param>
        /// <remarks>
        /// This constructor injects the repository-based virtual key service that will handle all
        /// the actual operations. The adapter simply delegates method calls to this implementation.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if virtualKeyService or logger is null</exception>
        public RepositoryVirtualKeyService(
            ConduitLLM.WebUI.Interfaces.IVirtualKeyService virtualKeyService,
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
            var validationInfo = await _virtualKeyService.ValidateVirtualKeyAsync(key, requestedModel);
            return validationInfo.ToEntity();
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
            var validationInfo = await _virtualKeyService.GetVirtualKeyInfoForValidationAsync(keyId, cancellationToken);
            return validationInfo.ToEntity();
        }
    }
}
