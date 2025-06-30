import { ConduitAdminClient } from '../src';
import { RefundSpendRequest } from '../src/models/virtualKey';

/**
 * Example demonstrating how to use the refund API functionality
 */
async function main() {
  // Initialize the client
  const client = new ConduitAdminClient({
    baseUrl: 'https://admin-api.example.com',
    apiKey: 'your-admin-api-key',
  });

  try {
    // Example 1: Simple refund
    await simpleRefundExample(client);

    // Example 2: Refund with transaction tracking
    await refundWithTransactionTrackingExample(client);

    // Example 3: Partial refund scenario
    await partialRefundExample(client);

    // Example 4: Error handling
    await errorHandlingExample(client);
  } catch (error) {
    console.error('Error:', error);
  }
}

async function simpleRefundExample(client: ConduitAdminClient) {
  console.log('=== Simple Refund Example ===');
  
  const virtualKeyId = 123; // Your virtual key ID
  
  // Create refund request
  const refundRequest: RefundSpendRequest = {
    amount: 10.50, // Refund $10.50
    reason: 'Service interruption - API was down for 2 hours',
  };

  // Issue refund
  await client.virtualKeys.refundSpend(virtualKeyId, refundRequest);
  
  console.log(`Refund of $${refundRequest.amount} issued successfully`);
  
  // Check updated balance
  const keyInfo = await client.virtualKeys.getById(virtualKeyId);
  console.log(`Updated spend: $${keyInfo.currentSpend}`);
}

async function refundWithTransactionTrackingExample(client: ConduitAdminClient) {
  console.log('\n=== Refund with Transaction Tracking Example ===');
  
  const virtualKeyId = 123;
  
  // Create refund with original transaction reference
  const refundRequest: RefundSpendRequest = {
    amount: 25.00,
    reason: 'Duplicate charge for image generation',
    originalTransactionId: 'txn_img_20240630_001', // Reference to original charge
  };

  await client.virtualKeys.refundSpend(virtualKeyId, refundRequest);
  
  console.log(`Refund issued with transaction tracking: ${refundRequest.originalTransactionId}`);
}

async function partialRefundExample(client: ConduitAdminClient) {
  console.log('\n=== Partial Refund Example ===');
  
  const virtualKeyId = 123;
  
  // Get current spend first
  const keyInfo = await client.virtualKeys.getById(virtualKeyId);
  const originalSpend = keyInfo.currentSpend;
  
  console.log(`Current spend before refund: $${originalSpend}`);
  
  // Calculate partial refund (e.g., 20% of a $50 charge)
  const originalCharge = 50.00;
  const refundPercentage = 0.20; // 20%
  const refundAmount = originalCharge * refundPercentage;
  
  const refundRequest: RefundSpendRequest = {
    amount: refundAmount,
    reason: `Partial refund - ${(refundPercentage * 100)}% credit for degraded service quality`,
    originalTransactionId: 'txn_api_20240630_002',
  };

  await client.virtualKeys.refundSpend(virtualKeyId, refundRequest);
  
  // Verify the refund
  const updatedKeyInfo = await client.virtualKeys.getById(virtualKeyId);
  const expectedSpend = originalSpend - refundAmount;
  
  console.log(`Partial refund of $${refundAmount} issued`);
  console.log(`New spend: $${updatedKeyInfo.currentSpend} (expected: $${expectedSpend})`);
}

async function errorHandlingExample(client: ConduitAdminClient) {
  console.log('\n=== Error Handling Example ===');
  
  const virtualKeyId = 123;
  
  try {
    // Attempt to refund more than current spend
    const keyInfo = await client.virtualKeys.getById(virtualKeyId);
    const excessiveRefund: RefundSpendRequest = {
      amount: keyInfo.currentSpend + 100, // Try to refund more than spent
      reason: 'Testing excessive refund validation',
    };

    await client.virtualKeys.refundSpend(virtualKeyId, excessiveRefund);
  } catch (error: any) {
    console.log('Expected error for excessive refund:', error.message);
  }

  try {
    // Attempt refund without reason
    const invalidRefund: RefundSpendRequest = {
      amount: 10.00,
      reason: '', // Empty reason should fail validation
    };

    await client.virtualKeys.refundSpend(virtualKeyId, invalidRefund);
  } catch (error: any) {
    console.log('Expected error for missing reason:', error.message);
  }
}

// Run the examples
if (require.main === module) {
  main().catch(console.error);
}