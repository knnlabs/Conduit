import { ConduitCoreClient } from '../src';

const getWeatherTool = {
  type: 'function' as const,
  function: {
    name: 'get_weather',
    description: 'Get the current weather in a given location',
    parameters: {
      type: 'object',
      properties: {
        location: {
          type: 'string',
          description: 'The city and state, e.g. San Francisco, CA',
        },
        unit: {
          type: 'string',
          enum: ['celsius', 'fahrenheit'],
          description: 'The unit of temperature',
        },
      },
      required: ['location'],
    },
  },
};

async function getWeather(location: string, unit: string = 'fahrenheit'): Promise<string> {
  return JSON.stringify({
    location,
    temperature: 72,
    unit,
    conditions: 'Sunny',
  });
}

async function main() {
  const client = new ConduitCoreClient({
    apiKey: process.env.CONDUIT_API_KEY || 'your-virtual-key',
    baseURL: process.env.CONDUIT_BASE_URL || 'https://api.conduit.ai',
  });

  try {
    console.log('Sending request with function calling...\n');

    const messages = [
      {
        role: 'user' as const,
        content: 'What\'s the weather like in San Francisco?',
      },
    ];

    const initialResponse = await client.chat.completions.create({
      model: 'gpt-4',
      messages,
      tools: [getWeatherTool],
      tool_choice: 'auto',
    });

    const responseMessage = initialResponse.choices[0].message;
    messages.push(responseMessage);

    if (responseMessage.tool_calls) {
      console.log('Model wants to call functions:');
      
      for (const toolCall of responseMessage.tool_calls) {
        console.log(`- ${toolCall.function.name}(${toolCall.function.arguments})`);
        
        const args = JSON.parse(toolCall.function.arguments);
        const result = await getWeather(args.location, args.unit);
        
        messages.push({
          role: 'tool',
          content: result,
          tool_call_id: toolCall.id,
        });
      }

      console.log('\nGetting final response with function results...\n');
      
      const finalResponse = await client.chat.completions.create({
        model: 'gpt-4',
        messages,
      });

      console.log('Final response:', finalResponse.choices[0].message.content);
    } else {
      console.log('Response:', responseMessage.content);
    }
  } catch (error) {
    console.error('Error:', error);
  }
}

if (require.main === module) {
  main();
}