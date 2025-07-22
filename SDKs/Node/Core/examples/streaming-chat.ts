import { ConduitCoreClient } from '../src';

async function main() {
  const client = new ConduitCoreClient({
    apiKey: process.env.CONDUIT_API_KEY || 'your-virtual-key',
    baseURL: process.env.CONDUIT_BASE_URL || 'https://api.conduit.ai',
  });

  try {
    console.log('Starting streaming chat completion...\n');
    
    const stream = await client.chat.completions.create({
      model: 'gpt-4',
      messages: [
        {
          role: 'user',
          content: 'Write a haiku about programming.',
        },
      ],
      stream: true,
      temperature: 0.8,
    });

    let fullContent = '';
    console.log('Response: ');
    
    for await (const chunk of stream) {
      const content = chunk.choices[0]?.delta?.content;
      if (content) {
        process.stdout.write(content);
        fullContent += content;
      }

      if (chunk.choices[0]?.finish_reason) {
        console.log('\n\nFinish reason:', chunk.choices[0].finish_reason);
      }

      if (chunk.usage) {
        console.log('\nToken usage:', chunk.usage);
      }

      if (chunk.performance) {
        console.log('Performance metrics:', chunk.performance);
      }
    }

    console.log('\n\nComplete response:', fullContent);
  } catch (error) {
    console.error('Streaming error:', error);
  }
}

if (require.main === module) {
  main();
}