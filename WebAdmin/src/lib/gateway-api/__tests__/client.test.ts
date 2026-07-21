import { GatewayClient, ModelCapability, RateLimitError } from "..";

describe("GatewayClient", () => {
  beforeEach(() => jest.clearAllMocks());
  afterEach(() => jest.restoreAllMocks());

  function jsonResponse(body: unknown, status = 200): Response {
    return {
      ok: status >= 200 && status < 300,
      status,
      statusText: status === 200 ? "OK" : "Error",
      headers: new Headers({ "content-type": "application/json" }),
      text: async () => JSON.stringify(body),
    } as Response;
  }

  it("sends the virtual key as an opaque Bearer token", async () => {
    const fetchMock = jest.spyOn(global, "fetch").mockResolvedValue(
      jsonResponse({ data: [], count: 0 }),
    );
    const client = new GatewayClient({
      apiKey: "vk_test",
      baseURL: "https://gateway.test/",
    });

    await client.discovery.getModelsByCapability(ModelCapability.Chat);

    const [request] = fetchMock.mock.calls[0];
    expect((request as Request).url).toBe(
      "https://gateway.test/v1/discovery/models?capability=chat",
    );
    expect((request as Request).headers.get("Authorization")).toBe("Bearer vk_test");
  });

  it("uses the supplied virtual key when minting an ephemeral key", async () => {
    const fetchMock = jest.spyOn(global, "fetch").mockResolvedValue(
      jsonResponse({
        ephemeralKey: "ephemeral",
        expiresAt: "2026-07-20T00:00:00Z",
        expiresInSeconds: 60,
      }),
    );
    const client = new GatewayClient({
      apiKey: "server-key",
      baseURL: "https://gateway.test",
    });

    await client.auth.generateEphemeralKey("webadmin-key", {
      metadata: { purpose: "test" },
    });

    const [request] = fetchMock.mock.calls[0];
    expect((request as Request).url).toBe("https://gateway.test/v1/auth/ephemeral-key");
    expect((request as Request).method).toBe("POST");
    expect((request as Request).headers.get("Authorization")).toBe("Bearer webadmin-key");
  });

  it("validates media limits without issuing a request", () => {
    const client = new GatewayClient({
      apiKey: "key",
      baseURL: "https://gateway.test",
    });
    const oversized = { size: 101 * 1024 * 1024, type: "image/png" } as Blob;
    expect(client.media.validateFileSize(oversized, "Image")).toEqual(
      expect.objectContaining({ valid: false }),
    );
  });

  it("normalizes Gateway error responses", async () => {
    jest.spyOn(global, "fetch").mockResolvedValue({
      ok: false,
      status: 429,
      statusText: "Too Many Requests",
      headers: new Headers([["retry-after", "7"]]),
      text: async () => JSON.stringify({ error: { message: "Slow down" } }),
    } as Response);
    const client = new GatewayClient({
      apiKey: "key",
      baseURL: "https://gateway.test",
    });

    await expect(client.discovery.getModels()).rejects.toEqual(
      expect.objectContaining({
        message: "Slow down",
        retryAfter: 7,
      }),
    );
    await expect(client.discovery.getModels()).rejects.toBeInstanceOf(
      RateLimitError,
    );
  });
});
