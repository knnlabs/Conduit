import type {
  ChatCompletionChunk,
  ChatStreamEvent,
  FinalMetrics,
  ReasoningEvent,
  StreamingErrorEvent,
  StreamingMetrics,
  ToolExecutingEvent,
  ToolResultEvent,
} from "./types";

export function isChatCompletionChunk(
  value: unknown,
): value is ChatCompletionChunk {
  return (
    typeof value === "object" &&
    value !== null &&
    (value as Record<string, unknown>).object === "chat.completion.chunk"
  );
}

export function isStreamingMetrics(value: unknown): value is StreamingMetrics {
  return (
    typeof value === "object" &&
    value !== null &&
    ["current_tokens_per_second", "tokens_generated", "elapsed_ms"].some(
      (key) => key in value,
    )
  );
}

export function isFinalMetrics(value: unknown): value is FinalMetrics {
  return (
    typeof value === "object" &&
    value !== null &&
    ["tokens_per_second", "total_latency_ms", "completion_tokens"].some(
      (key) => key in value,
    )
  );
}

export function isStreamingErrorEvent(
  value: unknown,
): value is StreamingErrorEvent {
  return (
    typeof value === "object" &&
    value !== null &&
    typeof (value as Record<string, unknown>).error === "string"
  );
}

export function isReasoningEvent(value: unknown): value is ReasoningEvent {
  return (
    typeof value === "object" &&
    value !== null &&
    typeof (value as Record<string, unknown>).content === "string" &&
    !("object" in value)
  );
}

export function isToolExecutingEvent(
  value: unknown,
): value is ToolExecutingEvent {
  return (
    typeof value === "object" &&
    value !== null &&
    typeof (value as Record<string, unknown>).status === "string"
  );
}

export function isToolResultEvent(value: unknown): value is ToolResultEvent {
  return (
    typeof value === "object" &&
    value !== null &&
    typeof (value as Record<string, unknown>).tool_call_id === "string" &&
    "result" in value
  );
}

function decodeEvent(
  eventName: string,
  data: string,
): ChatStreamEvent | undefined {
  if (data === "[DONE]" || eventName === "done") return undefined;
  let parsed: unknown;
  try {
    parsed = JSON.parse(data);
  } catch {
    parsed = data;
  }

  if (eventName === "reasoning" && typeof parsed === "string")
    return { content: parsed };
  if (eventName === "error" && typeof parsed === "string")
    return { error: parsed };
  return parsed as ChatStreamEvent;
}

export async function* parseGatewaySse(
  response: Response,
): AsyncGenerator<ChatStreamEvent> {
  if (!response.body)
    throw new Error("Gateway returned an empty streaming response");
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  for (;;) {
    const { done, value } = await reader.read();
    buffer += decoder.decode(value, { stream: !done }).replace(/\r\n/g, "\n");
    let boundary = buffer.indexOf("\n\n");
    while (boundary >= 0) {
      const block = buffer.slice(0, boundary);
      buffer = buffer.slice(boundary + 2);
      let eventName = "message";
      const data: string[] = [];
      for (const line of block.split("\n")) {
        if (line.startsWith("event:")) eventName = line.slice(6).trim();
        else if (line.startsWith("data:")) data.push(line.slice(5).trimStart());
      }
      if (data.length > 0) {
        const decoded = decodeEvent(eventName, data.join("\n"));
        if (decoded !== undefined) yield decoded;
      }
      boundary = buffer.indexOf("\n\n");
    }
    if (done) break;
  }

  if (buffer.trim()) {
    const data = buffer
      .split("\n")
      .filter((line) => line.startsWith("data:"))
      .map((line) => line.slice(5).trimStart());
    if (data.length > 0) {
      const decoded = decodeEvent("message", data.join("\n"));
      if (decoded !== undefined) yield decoded;
    }
  }
}
