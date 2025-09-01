# Testing Reasoning Visual Treatment

## Summary of Changes

We've successfully implemented visual treatment for reasoning chunks from models like Groq's gpt-oss-120b:

### 1. Backend Changes
- Modified `DeltaContent.cs` to include `Reasoning` and `Channel` fields
- Updated streaming client to pass through raw JSON without filtering
- Preserved all provider-specific fields using `JsonExtensionData`

### 2. SDK Changes  
- Updated TypeScript types to include reasoning fields
- Modified `ChatStreamingManager` to handle reasoning chunks
- Reasoning content is accumulated as part of the total content

### 3. UI Changes
- Added `streamingChannel` state to track the current streaming channel
- Modified `ChatStreamingLogic` to capture channel from chunks
- Updated `ChatMessages` to apply CSS class when channel is "analysis"
- Added CSS styles for reasoning content:
  - Italic text with reduced opacity
  - "🤔 Reasoning:" prefix
  - Subtle blue gradient background
  - Left border for visual separation

## Testing Instructions

1. Navigate to http://localhost:3000/chat
2. Select a Groq model (e.g., "llama-3.1-70b-versatile" or if available "gpt-oss-120b")
3. Ask a complex question that would trigger reasoning, like:
   - "What is the history of France?"
   - "Explain quantum computing step by step"
   - "What are the pros and cons of different programming paradigms?"

## Expected Behavior

When using a model that sends reasoning chunks (channel: "analysis"):
- Reasoning text should appear in italics
- Text should be slightly dimmed (85% opacity)
- A "🤔 Reasoning:" prefix should appear
- Left border and subtle background gradient
- After reasoning, the final answer should appear in normal formatting

## Visual Indicators

### Light Mode
- Reasoning: Gray italic text with blue accent
- Normal content: Black text

### Dark Mode  
- Reasoning: Dimmed tertiary text color
- Normal content: Primary text color

## Architecture Notes

The system now follows a "Smart Proxy" pattern:
- **Transparent by default**: All provider data passes through
- **Observable**: We can detect and track reasoning vs content
- **Intervenable**: We apply visual treatment without filtering
- **Evolvable**: New provider fields automatically pass through via ExtensionData