/**
 * This example demonstrates the improvements in type safety
 * between the old SDK (with any/unknown) and the new type-safe SDK
 */

// ============================================
// OLD APPROACH - Many any/unknown types
// ============================================

// Old client with loose typing
async function oldApproach() {
  const client = new OldConduitClient({
    apiKey: 'your-api-key'
  });

  // Problem 1: No autocomplete for request properties
  // Problem 2: Can pass wrong types without compile-time errors
  // Problem 3: Response is typed as 'any'
  const response = await client.chat.create({
    model: 'gpt-4',
    messages: [
      {
        role: 'usr', // Typo! Should be 'user' - not caught
        content: 'Hello'
      }
    ],
    temperature: '0.7', // Wrong type! Should be number - not caught
    max_tokens: '100', // Wrong type! Should be number - not caught
  } as any); // Had to cast to any to avoid errors

  // Problem 4: No type safety on response
  console.log(response.choice[0].mesage.content); // Typos not caught!
  
  // Problem 5: Error handling with unknown types
  try {
    await client.chat.create({} as any);
  } catch (error: any) { // Using any for errors
    console.log(error.response?.data?.error); // No type safety
  }
}

// ============================================
// NEW APPROACH - Full type safety
// ============================================

import { ConduitCoreClient } from '../src';
import type { components } from '../src/generated/core-api';

// Type aliases for clarity
type ChatRequest = components['schemas']['ChatCompletionRequest'];
type ChatResponse = components['schemas']['ChatCompletionResponse'];

async function newApproach() {
  const client = new ConduitCoreClient({
    apiKey: 'your-api-key'
  });

  // Benefit 1: Full autocomplete for all properties
  // Benefit 2: Type checking prevents invalid values
  // Benefit 3: Response is properly typed
  const response = await client.chat.create({
    model: 'gpt-4',
    messages: [
      {
        role: 'user', // Autocomplete shows: 'system' | 'user' | 'assistant'
        content: 'Hello'
      }
    ],
    temperature: 0.7, // Type error if you try to pass a string
    max_tokens: 100, // Type error if you try to pass a string
  });

  // Benefit 4: Full type safety on response
  console.log(response.choices[0].message.content); // All properties have autocomplete
  
  // Benefit 5: Typed error handling
  try {
    await client.chat.create({
      model: 'gpt-4',
      messages: [] // Empty messages array
    });
  } catch (error) {
    if (client.isConduitError(error)) {
      // error is now typed as ConduitError
      console.log(error.code); // Autocomplete shows available error codes
      console.log(error.statusCode); // Type-safe access to status code
    }
  }
}

// ============================================
// ADVANCED TYPE SAFETY EXAMPLES
// ============================================

async function advancedExamples() {
  const client = new ConduitCoreClient({
    apiKey: 'your-api-key'
  });

  // Example 1: Function calling with full type safety
  const functionResponse = await client.chat.create({
    model: 'gpt-4',
    messages: [{ role: 'user', content: 'What\'s the weather in Paris?' }],
    tools: [
      {
        type: 'function', // Autocomplete shows only 'function'
        function: {
          name: 'get_weather',
          description: 'Get weather for a location',
          parameters: {
            type: 'object',
            properties: {
              location: { type: 'string' },
              unit: { type: 'string', enum: ['celsius', 'fahrenheit'] }
            },
            required: ['location']
          }
        }
      }
    ],
    tool_choice: 'auto' // Autocomplete shows: 'none' | 'auto' | object
  });

  // Type-safe access to tool calls
  const toolCall = functionResponse.choices[0].message.tool_calls?.[0];
  if (toolCall && toolCall.type === 'function') {
    console.log(toolCall.function.name); // Type-safe
    console.log(toolCall.function.arguments); // Type-safe as string
  }

  // Example 2: Streaming with proper types
  const stream = await client.chat.create({
    model: 'gpt-4',
    messages: [{ role: 'user', content: 'Tell me a story' }],
    stream: true // This changes the return type!
  });

  // stream is now typed as StreamingResponse<ChatCompletionChunk>
  for await (const chunk of stream) {
    // chunk is typed as ChatCompletionChunk
    const content = chunk.choices[0]?.delta?.content;
    if (content) {
      process.stdout.write(content);
    }
  }

  // Example 3: Image generation with type safety
  const image = await client.images.generate({
    prompt: 'A beautiful sunset',
    n: 1, // Number of images
    size: '1024x1024', // Autocomplete shows valid sizes
    response_format: 'url' // Autocomplete shows: 'url' | 'b64_json'
  });

  // Type-safe access to image data
  console.log(image.data[0].url); // All properties typed

  // Example 4: Embeddings with type safety
  const embeddings = await client.embeddings.create({
    model: 'text-embedding-3-small',
    input: ['Hello world', 'How are you?'],
    encoding_format: 'float' // Autocomplete shows options
  });

  // Type-safe access to embedding vectors
  embeddings.data.forEach(embedding => {
    console.log(embedding.embedding); // Type: number[]
    console.log(embedding.index); // Type: number
  });
}

// ============================================
// TYPE SAFETY IN ERROR SCENARIOS
// ============================================

async function errorHandlingExamples() {
  const client = new ConduitCoreClient({
    apiKey: 'your-api-key'
  });

  try {
    await client.chat.create({
      model: 'gpt-4',
      messages: [{ role: 'user', content: 'Hello' }],
      // @ts-expect-error - This will cause a type error
      invalid_param: 'value' // Type error: Object literal may only specify known properties
    });
  } catch (error) {
    // Proper error type guards
    if (client.isAuthError(error)) {
      // Handle auth errors - error.code is typed
      console.log('Auth failed:', error.code);
    } else if (client.isRateLimitError(error)) {
      // Handle rate limits - error.retryAfter is typed
      console.log('Rate limited, retry after:', error.retryAfter);
    } else if (client.isValidationError(error)) {
      // Handle validation errors - error.details is typed
      console.log('Validation failed:', error.details);
    }
  }
}

// ============================================
// BENEFITS SUMMARY
// ============================================

/**
 * Benefits of the new type-safe approach:
 * 
 * 1. **Compile-time Safety**: Catch errors before runtime
 * 2. **IDE Support**: Full autocomplete and IntelliSense
 * 3. **Self-documenting**: Types serve as inline documentation
 * 4. **Refactoring Safety**: Change detection across codebase
 * 5. **Reduced Bugs**: No more typos or wrong types
 * 6. **Better DX**: Developers know exactly what's expected
 * 7. **API Contract**: Clear contract between client and server
 * 8. **Type Guards**: Proper error discrimination
 * 9. **Generic Constraints**: Flexible but safe APIs
 * 10. **Future Proof**: Easy to update when API changes
 */