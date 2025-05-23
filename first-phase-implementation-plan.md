# Phase 1 Implementation: Complete API Client and Adapters

Based on the analysis of the current codebase, this document outlines the specific changes needed to complete the API client implementation and adapter patterns for the WebUI project.

## 1. Missing API Endpoints in Admin API

The following endpoints need to be added to the Admin API to support the WebUI adapters:

### 1.1. VirtualKeysController Additional Endpoints

The VirtualKeysController already has:
- GET /api/virtualkeys - List all keys
- GET /api/virtualkeys/{id} - Get a specific key
- POST /api/virtualkeys - Create a new key
- PUT /api/virtualkeys/{id} - Update a key
- DELETE /api/virtualkeys/{id} - Delete a key
- POST /api/virtualkeys/{id}/reset-spend - Reset spending for a key

The following new endpoints need to be added:

1. **Validate Virtual Key**
   ```csharp
   [HttpPost("validate")]
   [ProducesResponseType(typeof(VirtualKeyValidationResult), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status400BadRequest)]
   [ProducesResponseType(StatusCodes.Status401Unauthorized)]
   public async Task<IActionResult> ValidateKey([FromBody] ValidateVirtualKeyRequest request)
   ```
   
2. **Update Spend**
   ```csharp
   [HttpPost("{id}/spend")]
   [ProducesResponseType(StatusCodes.Status204NoContent)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> UpdateSpend(int id, [FromBody] UpdateSpendRequest request)
   ```
   
3. **Check Budget Period**
   ```csharp
   [HttpPost("{id}/check-budget")]
   [ProducesResponseType(typeof(BudgetCheckResult), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> CheckBudget(int id)
   ```
   
4. **Get Validation Info**
   ```csharp
   [HttpGet("{id}/validation-info")]
   [Authorize(Policy = "MasterKeyPolicy")]
   [ProducesResponseType(typeof(VirtualKeyValidationInfoDto), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> GetValidationInfo(int id)
   ```

### 1.2. IpFilterController Additional Endpoints

Add a high-performance IP filter check endpoint:

```csharp
[HttpGet("check/{ipAddress}")]
[ProducesResponseType(typeof(IpCheckResult), StatusCodes.Status200OK)]
public async Task<IActionResult> CheckIpAddress(string ipAddress)
```

## 2. Required DTO Classes

Create the following DTO classes to support the new endpoints:

### 2.1. VirtualKey DTOs

1. **ValidateVirtualKeyRequest**
   ```csharp
   public class ValidateVirtualKeyRequest
   {
       [Required]
       public string Key { get; set; }
       
       public string? RequestedModel { get; set; }
   }
   ```

2. **VirtualKeyValidationResult**
   ```csharp
   public class VirtualKeyValidationResult
   {
       public bool IsValid { get; set; }
       public int? VirtualKeyId { get; set; }
       public string? KeyName { get; set; }
       public string? AllowedModels { get; set; }
       public decimal? MaxBudget { get; set; }
       public decimal CurrentSpend { get; set; }
       public string? ErrorMessage { get; set; }
   }
   ```

3. **UpdateSpendRequest**
   ```csharp
   public class UpdateSpendRequest
   {
       [Required]
       public decimal Cost { get; set; }
   }
   ```

4. **BudgetCheckResult**
   ```csharp
   public class BudgetCheckResult
   {
       public bool WasReset { get; set; }
       public DateTime? NewBudgetStartDate { get; set; }
   }
   ```

5. **VirtualKeyValidationInfoDto**
   ```csharp
   public class VirtualKeyValidationInfoDto
   {
       public int Id { get; set; }
       public string KeyName { get; set; }
       public string? AllowedModels { get; set; }
       public decimal? MaxBudget { get; set; }
       public decimal CurrentSpend { get; set; }
       public string? BudgetDuration { get; set; }
       public DateTime? BudgetStartDate { get; set; }
       public bool IsEnabled { get; set; }
       public DateTime? ExpiresAt { get; set; }
       public int? RateLimitRpm { get; set; }
       public int? RateLimitRpd { get; set; }
   }
   ```

### 2.2. IP Filter DTOs

1. **IpCheckResult**
   ```csharp
   public class IpCheckResult
   {
       public bool IsAllowed { get; set; }
       public string? DeniedReason { get; set; }
   }
   ```

## 3. Update IAdminVirtualKeyService Interface

Extend the `IAdminVirtualKeyService` interface to include the new methods:

```csharp
public interface IAdminVirtualKeyService
{
    // Existing methods...
    
    /// <summary>
    /// Validates a virtual key
    /// </summary>
    /// <param name="key">The virtual key to validate</param>
    /// <param name="requestedModel">Optional model being requested</param>
    /// <returns>Validation result with information about the key</returns>
    Task<VirtualKeyValidationResult> ValidateVirtualKeyAsync(string key, string? requestedModel = null);
    
    /// <summary>
    /// Updates the spend amount for a virtual key
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <param name="cost">The cost to add to the current spend</param>
    /// <returns>True if the update was successful, false otherwise</returns>
    Task<bool> UpdateSpendAsync(int id, decimal cost);
    
    /// <summary>
    /// Checks if the budget period has expired and resets if needed
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <returns>Result indicating if a reset was performed</returns>
    Task<BudgetCheckResult> CheckBudgetAsync(int id);
    
    /// <summary>
    /// Gets detailed information about a virtual key for validation purposes
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <returns>Virtual key validation information or null if not found</returns>
    Task<VirtualKeyValidationInfoDto?> GetValidationInfoAsync(int id);
}
```

## 4. Update the AdminVirtualKeyService Implementation

Implement the new methods in the `AdminVirtualKeyService` class.

## 5. Update IAdminApiClient Interface

Add the new methods to the `IAdminApiClient` interface in the WebUI project:

```csharp
public interface IAdminApiClient
{
    // Existing methods...
    
    // Virtual key endpoints
    Task<VirtualKeyValidationResult?> ValidateVirtualKeyAsync(string key, string? requestedModel = null);
    Task<bool> UpdateVirtualKeySpendAsync(int id, decimal cost);
    Task<BudgetCheckResult?> CheckVirtualKeyBudgetAsync(int id);
    Task<VirtualKeyValidationInfoDto?> GetVirtualKeyValidationInfoAsync(int id);
    
    // IP Filter endpoints
    Task<IpCheckResult?> CheckIpAddressAsync(string ipAddress);
}
```

## 6. Update AdminApiClient Implementation

Add implementations for the new methods in the `AdminApiClient` class.

## 7. Update VirtualKeyServiceAdapter

Update the `VirtualKeyServiceAdapter` class to use the new API client methods:

```csharp
public class VirtualKeyServiceAdapter : IVirtualKeyService
{
    // Existing methods...
    
    public async Task<VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
    {
        try
        {
            var result = await _adminApiClient.ValidateVirtualKeyAsync(key, requestedModel);
            
            if (result == null || !result.IsValid || !result.VirtualKeyId.HasValue)
            {
                return null;
            }
            
            // Map from validation result to VirtualKey entity
            var virtualKey = new VirtualKey
            {
                Id = result.VirtualKeyId.Value,
                KeyName = result.KeyName,
                AllowedModels = result.AllowedModels,
                MaxBudget = result.MaxBudget,
                CurrentSpend = result.CurrentSpend,
                IsEnabled = true, // If we got here, it's enabled
                // Other properties as needed for validation
            };
            
            return virtualKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating virtual key");
            return null;
        }
    }
    
    public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
    {
        try
        {
            return await _adminApiClient.UpdateVirtualKeySpendAsync(keyId, cost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating spend for virtual key {KeyId}", keyId);
            return false;
        }
    }
    
    public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _adminApiClient.CheckVirtualKeyBudgetAsync(keyId);
            return result?.WasReset ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking budget expiration for virtual key {KeyId}", keyId);
            return false;
        }
    }
    
    public async Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await _adminApiClient.GetVirtualKeyValidationInfoAsync(keyId);
            
            if (info == null)
                return null;
            
            // Map from DTO to entity
            return new VirtualKey
            {
                Id = info.Id,
                KeyName = info.KeyName,
                AllowedModels = info.AllowedModels,
                MaxBudget = info.MaxBudget,
                CurrentSpend = info.CurrentSpend,
                BudgetDuration = info.BudgetDuration,
                BudgetStartDate = info.BudgetStartDate,
                IsEnabled = info.IsEnabled,
                ExpiresAt = info.ExpiresAt,
                RateLimitRpm = info.RateLimitRpm,
                RateLimitRpd = info.RateLimitRpd
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual key {KeyId} for validation", keyId);
            return null;
        }
    }
}
```

## 8. Update IpFilterServiceAdapter

Update the IpFilterServiceAdapter to use the high-performance IP check endpoint.

## 9. Implementation Steps

1. Create the new DTO classes in the Configuration project
2. Update the IAdminVirtualKeyService interface with the new methods
3. Implement the new methods in AdminVirtualKeyService
4. Create the controller endpoints in VirtualKeysController
5. Update the IAdminApiClient interface with the new methods
6. Implement the new methods in AdminApiClient
7. Update the VirtualKeyServiceAdapter to use the new API client methods
8. Update IpFilterServiceAdapter for improved performance
9. Test all the changes thoroughly

## 10. Testing

Create unit tests for:
1. The new controller endpoints
2. The service implementations
3. The adapter implementations

Create integration tests for:
1. The API client against a running Admin API
2. The WebUI using the adapters with the Admin API

## 11. Completion Criteria

The first phase implementation will be considered complete when:

1. All the missing API endpoints are implemented in the Admin API
2. The AdminApiClient in WebUI has implementations for all the required methods
3. All adapter implementations are updated to use the API client
4. All tests pass
5. The system works correctly with CONDUIT_USE_ADMIN_API=true

After completing these changes, we will proceed to Phase 2: Standardizing on the Admin API.