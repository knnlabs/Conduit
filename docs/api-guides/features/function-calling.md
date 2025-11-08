# Function Calling Guide

This guide explains how to use function calling (tool use) in Conduit, both through the API and the WebUI Chat Playground.

## Overview

Function calling allows LLMs to request the execution of specific functions during a conversation. The LLM doesn't execute the functions directly - it only requests them. Your application is responsible for executing the functions and returning the results.

## Function Calling Flow

1. **Define Functions**: You provide function definitions (name, description, parameters) with your request
2. **LLM Decides**: Based on the conversation, the LLM determines if it needs to call any functions
3. **Function Request**: The LLM returns a response with `tool_calls` indicating which functions to execute
4. **Execute Functions**: Your application executes the requested functions
5. **Return Results**: Send the function results back as a new message with role `tool`
6. **Final Response**: The LLM uses the function results to generate its final response

## API Usage

### Step 1: Define Your Functions

```json
{
  "model": "gpt-4",
  "messages": [
    {
      "role": "user",
      "content": "What's the weather like in Boston?"
    }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "get_weather",
        "description": "Get the current weather for a location",
        "parameters": {
          "type": "object",
          "properties": {
            "location": {
              "type": "string",
              "description": "The city and state, e.g. San Francisco, CA"
            },
            "unit": {
              "type": "string",
              "enum": ["celsius", "fahrenheit"],
              "description": "Temperature unit"
            }
          },
          "required": ["location"]
        }
      }
    }
  ],
  "tool_choice": "auto"
}
```

### Step 2: Handle Function Calls in Response

The API will return a response with `tool_calls`:

```json
{
  "choices": [
    {
      "message": {
        "role": "assistant",
        "content": null,
        "tool_calls": [
          {
            "id": "call_abc123",
            "type": "function",
            "function": {
              "name": "get_weather",
              "arguments": "{\"location\": \"Boston, MA\", \"unit\": \"fahrenheit\"}"
            }
          }
        ]
      }
    }
  ]
}
```

### Step 3: Execute and Return Results

Execute the function and send the result back:

```json
{
  "model": "gpt-4",
  "messages": [
    {
      "role": "user",
      "content": "What's the weather like in Boston?"
    },
    {
      "role": "assistant",
      "content": null,
      "tool_calls": [
        {
          "id": "call_abc123",
          "type": "function",
          "function": {
            "name": "get_weather",
            "arguments": "{\"location\": \"Boston, MA\", \"unit\": \"fahrenheit\"}"
          }
        }
      ]
    },
    {
      "role": "tool",
      "content": "{\"temperature\": 72, \"condition\": \"sunny\", \"humidity\": 45}",
      "tool_call_id": "call_abc123"
    }
  ]
}
```

The LLM will then provide a final response using the function results.

## WebUI Chat Playground

The Chat Playground provides a visual interface for testing function calling:

### 1. Enable Function Calling

In the right sidebar, toggle "Enable Functions" to activate function calling.

### 2. Define Functions

Click "Add Function" to define available functions:

- **Name**: Function identifier (e.g., `get_weather`)
- **Description**: What the function does
- **Parameters**: JSON Schema defining the function's parameters

### 3. Demo Functions

The playground includes demo functions you can enable:
- `get_weather` - Weather information (simulated)
- `calculate` - Mathematical calculations

### 4. Function Call Flow

When the LLM requests a function:
1. A "Function Call Requested" card appears in the chat
2. Click "Input Result" to provide the function's output
3. Enter the result as JSON
4. The conversation continues with the function result

### 5. Manual Testing

The playground doesn't execute functions - you manually provide results. This allows you to:
- Test function calling flow without implementing functions
- Simulate different function responses
- Debug your function definitions

## Supported Providers

Function calling is supported by providers that implement the OpenAI-compatible API:
- OpenAI (GPT-4, GPT-3.5)
- Anthropic (Claude via OpenAI-compatible endpoint)
- Azure OpenAI
- Local providers with function calling support

## Best Practices

1. **Clear Function Names**: Use descriptive names that indicate the function's purpose
2. **Detailed Descriptions**: Help the LLM understand when to use each function
3. **Strict Schemas**: Define parameter schemas precisely to avoid errors
4. **Error Handling**: Always handle cases where function execution fails
5. **Security**: Never expose sensitive operations through functions

## Example: Multi-Function Chat

```python
# Python example using the Conduit API
import requests
import json

# Define multiple functions
tools = [
    {
        "type": "function",
        "function": {
            "name": "get_weather",
            "description": "Get current weather for a location",
            "parameters": {
                "type": "object",
                "properties": {
                    "location": {"type": "string"}
                },
                "required": ["location"]
            }
        }
    },
    {
        "type": "function", 
        "function": {
            "name": "search_web",
            "description": "Search the web for information",
            "parameters": {
                "type": "object",
                "properties": {
                    "query": {"type": "string"}
                },
                "required": ["query"]
            }
        }
    }
]

# Make request with functions
response = requests.post(
    "http://localhost:3000/v1/chat/completions",
    headers={"Authorization": "Bearer YOUR_KEY"},
    json={
        "model": "gpt-4",
        "messages": [{"role": "user", "content": "What's the weather in NYC and find news about it"}],
        "tools": tools
    }
)

# Handle function calls
# ... execute functions and continue conversation ...
```

## Troubleshooting

### Functions Not Being Called
- Ensure function descriptions clearly indicate their purpose
- Check that the model supports function calling
- Verify your function definitions are valid JSON Schema

### Invalid Function Calls
- Validate parameter schemas match expected format
- Ensure required fields are properly marked
- Check function names follow naming conventions (letters, numbers, _, -)

### Provider Errors
- Not all models support function calling
- Some providers may have different formats
- Check provider documentation for specific requirements