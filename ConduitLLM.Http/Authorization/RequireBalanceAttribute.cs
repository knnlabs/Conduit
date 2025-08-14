using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Http.Authorization
{
    /// <summary>
    /// Authorization attribute that ensures the virtual key has sufficient balance.
    /// This attribute should be applied to endpoints that consume credits/cost money.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireBalanceAttribute : Attribute, IAsyncAuthorizationFilter
    {
        /// <summary>
        /// Performs balance-based authorization for the request
        /// </summary>
        /// <param name="context">The authorization filter context</param>
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Check if user is authenticated and has VirtualKey claim
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Authentication required" });
                return;
            }

            try
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RequireBalanceAttribute>>();
                var virtualKeyService = context.HttpContext.RequestServices.GetRequiredService<IVirtualKeyService>();
                
                // Get the virtual key claim - this now works for both regular and ephemeral keys
                var virtualKeyClaim = context.HttpContext.User.FindFirst("VirtualKey");
                
                if (virtualKeyClaim == null)
                {
                    logger.LogWarning("No virtual key claim found in authenticated user for balance check");
                    context.Result = new UnauthorizedObjectResult(new { error = "Virtual key not found in authentication context" });
                    return;
                }

                var virtualKey = virtualKeyClaim.Value;
                
                // Extract model from route values if present
                string? requestedModel = null;
                if (context.HttpContext.Request.RouteValues.TryGetValue("model", out var modelValue))
                {
                    requestedModel = modelValue?.ToString();
                }
                
                // Validate the virtual key with full balance check
                // This works for both regular virtual keys and ephemeral keys (which now provide the real virtual key)
                var keyEntity = await virtualKeyService.ValidateVirtualKeyAsync(virtualKey, requestedModel);
                if (keyEntity == null)
                {
                    logger.LogWarning("Virtual key validation failed during balance check for key: {KeyPrefix}...", 
                        virtualKey.Length > 10 ? virtualKey.Substring(0, 10) : virtualKey);
                    
                    // Return 402 Payment Required for insufficient balance
                    context.Result = new ObjectResult(new { 
                        error = "Insufficient balance", 
                        message = "Your account balance is insufficient to perform this operation.",
                        statusCode = 402
                    })
                    {
                        StatusCode = StatusCodes.Status402PaymentRequired
                    };
                    return;
                }

                // Store validated key entity in HttpContext for potential use by controllers
                context.HttpContext.Items["ValidatedVirtualKey"] = keyEntity;
                
                // Check if this was originally an ephemeral key (for logging purposes)
                var isEphemeralKey = context.HttpContext.Items.ContainsKey("IsEphemeralKey") && 
                                    (bool)context.HttpContext.Items["IsEphemeralKey"]!;
                
                if (isEphemeralKey)
                {
                    logger.LogDebug("Balance check passed for ephemeral key using virtual key: {KeyName} (ID: {KeyId}), Balance: {Balance}", 
                        keyEntity.KeyName?.Replace(Environment.NewLine, "") ?? "Unknown", 
                        keyEntity.Id,
                        keyEntity.VirtualKeyGroup?.Balance ?? 0);
                }
                else
                {
                    logger.LogDebug("Balance check passed for virtual key: {KeyName} (ID: {KeyId})", 
                        keyEntity.KeyName?.Replace(Environment.NewLine, "") ?? "Unknown", keyEntity.Id);
                }
            }
            catch (Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<RequireBalanceAttribute>>();
                logger?.LogError(ex, "Error during balance authorization check");
                
                context.Result = new ObjectResult(new { 
                    error = "Authorization error", 
                    message = "An error occurred while checking account balance.",
                    statusCode = 500
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }
}