import { parseGatewaySse } from "../streaming";
import { TextDecoder } from "node:util";

Object.assign(globalThis, { TextDecoder });

function responseFromChunks(chunks: string[]): Response {
  const encoded = chunks.map((chunk) => Buffer.from(chunk));
  let index = 0;
  return {
    body: {
      getReader: () => ({
        read: async () =>
          index < encoded.length
            ? { done: false, value: encoded[index++] }
            : { done: true, value: undefined },
      }),
    },
  } as unknown as Response;
}

describe("parseGatewaySse", () => {
  it("parses fragmented content and named Conduit events", async () => {
    const response = responseFromChunks([
      'data: {"id":"1","object":"chat.completion.',
      'chunk","created":1,"model":"m","choices":[]}\n\n',
      'event: reasoning\ndata: {"content":"thinking"}\n\n',
      'event: metrics-final\ndata: {"total_tokens":3,"tokens_per_second":2}\n\n',
      "data: [DONE]\n\n",
    ]);

    const events = [];
    for await (const event of parseGatewaySse(response)) events.push(event);

    expect(events).toHaveLength(3);
    expect(events[0]).toEqual(
      expect.objectContaining({ object: "chat.completion.chunk" }),
    );
    expect(events[1]).toEqual({ content: "thinking" });
    expect(events[2]).toEqual(expect.objectContaining({ total_tokens: 3 }));
  });

  it("joins multiline data fields before decoding JSON", async () => {
    const response = responseFromChunks([
      'event: error\ndata: {"error":\ndata: "provider failed"}\n\n',
    ]);
    const events = [];
    for await (const event of parseGatewaySse(response)) events.push(event);
    expect(events).toEqual([{ error: "provider failed" }]);
  });
});
