import { ConduitCoreClient } from '../src';

async function main() {
  const client = new ConduitCoreClient({
    apiKey: process.env.CONDUIT_API_KEY || 'your-virtual-key',
    baseURL: process.env.CONDUIT_BASE_URL || 'https://api.conduit.ai',
  });

  try {
    console.log('Sending chat completion request...');
    
    const response = await client.chat.completions.create({
      model: 'gpt-4',
      messages: [
        {
          role: 'system',
          content: 'You are a helpful assistant.',
        },
        {
          role: 'user',
          content: 'What is the capital of France?',
        },
      ],
      temperature: 0.7,
      max_tokens: 100,
    });

    console.log('\nResponse:');
    console.log('ID:', response.id);
    console.log('Model:', response.model);
    console.log('Content:', response.choices[0].message.content);
    console.log('\nUsage:');
    console.log('- Prompt tokens:', response.usage.prompt_tokens);
    console.log('- Completion tokens:', response.usage.completion_tokens);
    console.log('- Total tokens:', response.usage.total_tokens);

    if (response.performance) {
      console.log('\nPerformance:');
      console.log('- Provider:', response.performance.provider_name);
      console.log('- Response time:', response.performance.provider_response_time_ms, 'ms');
      console.log('- Tokens/second:', response.performance.tokens_per_second);
    }
  } catch (error) {
    console.error('Error:', error);
  }
}

if (require.main === module) {
  main();
}