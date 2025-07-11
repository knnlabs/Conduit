using System;
using System.Threading.Tasks;
using ConduitLLM.AdminClient;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.AdminClient.Examples
{
    /// <summary>
    /// Example demonstrating how to use the refund API functionality.
    /// </summary>
    public class RefundExample
    {
        public static async Task RunAsync()
        {
            // Create client using factory method
            var client = ConduitAdminClient.Create(
                masterKey: "your-master-key",
                adminApiUrl: "https://admin-api.example.com"
            );

            try
            {
                // Example 1: Simple refund
                await SimpleRefundExample(client);

                // Example 2: Refund with transaction tracking
                await RefundWithTransactionTrackingExample(client);

                // Example 3: Partial refund scenario
                await PartialRefundExample(client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task SimpleRefundExample(ConduitAdminClient client)
        {
            Console.WriteLine("=== Simple Refund Example ===");
            
            var virtualKeyId = 123; // Your virtual key ID
            
            // Create refund request
            var refundRequest = new RefundSpendRequest
            {
                Amount = 10.50m, // Refund $10.50
                Reason = "Service interruption - API was down for 2 hours"
            };

            // Issue refund
            await client.VirtualKeys.RefundSpendAsync(virtualKeyId, refundRequest);
            
            Console.WriteLine($"Refund of ${refundRequest.Amount} issued successfully");
            
            // Check updated balance
            var keyInfo = await client.VirtualKeys.GetByIdAsync(virtualKeyId);
            Console.WriteLine($"Updated spend: ${keyInfo.CurrentSpend}");
        }

        private static async Task RefundWithTransactionTrackingExample(ConduitAdminClient client)
        {
            Console.WriteLine("\n=== Refund with Transaction Tracking Example ===");
            
            var virtualKeyId = 123;
            
            // Create refund with original transaction reference
            var refundRequest = new RefundSpendRequest
            {
                Amount = 25.00m,
                Reason = "Duplicate charge for image generation",
                OriginalTransactionId = "txn_img_20240630_001" // Reference to original charge
            };

            await client.VirtualKeys.RefundSpendAsync(virtualKeyId, refundRequest);
            
            Console.WriteLine($"Refund issued with transaction tracking: {refundRequest.OriginalTransactionId}");
        }

        private static async Task PartialRefundExample(ConduitAdminClient client)
        {
            Console.WriteLine("\n=== Partial Refund Example ===");
            
            var virtualKeyId = 123;
            
            // Get current spend first
            var keyInfo = await client.VirtualKeys.GetByIdAsync(virtualKeyId);
            var originalSpend = keyInfo.CurrentSpend;
            
            Console.WriteLine($"Current spend before refund: ${originalSpend}");
            
            // Calculate partial refund (e.g., 20% of a $50 charge)
            var originalCharge = 50.00m;
            var refundPercentage = 0.20m; // 20%
            var refundAmount = originalCharge * refundPercentage;
            
            var refundRequest = new RefundSpendRequest
            {
                Amount = refundAmount,
                Reason = $"Partial refund - {refundPercentage:P0} credit for degraded service quality",
                OriginalTransactionId = "txn_api_20240630_002"
            };

            await client.VirtualKeys.RefundSpendAsync(virtualKeyId, refundRequest);
            
            // Verify the refund
            keyInfo = await client.VirtualKeys.GetByIdAsync(virtualKeyId);
            var expectedSpend = originalSpend - refundAmount;
            
            Console.WriteLine($"Partial refund of ${refundAmount} issued");
            Console.WriteLine($"New spend: ${keyInfo.CurrentSpend} (expected: ${expectedSpend})");
        }
    }
}