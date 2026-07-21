import { GatewayClient, ModelCapability, RateLimitError } from "..";

describe("GatewayClient", () => {
  beforeEach(() => jest.clearAllMocks());
  afterEach(() => jest.restoreAllMocks());

  it("sends the virtual key as an opaque Bearer token", async () => {
    const fetchMock = jest.spyOn(global, "fetch").mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ data: [], count: 0 }),
    } as Response);
    const client = new GatewayClient({
      apiKey: "vk_test",
      baseURL: "https://gateway.test/",
    });

    await client.discovery.getModelsByCapability(ModelCapability.Chat);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe(
      "https://gateway.test/v1/discovery/models?capability=chat",
    );
    expect(init?.headers).toEqual(
      expect.objectContaining({ Authorization: "Bearer vk_test" }),
    );
  });

  it("uses the supplied virtual key when minting an ephemeral key", async () => {
    const fetchMock = jest.spyOn(global, "fetch").mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        ephemeralKey: "ephemeral",
        expiresAt: "date",
        expiresInSeconds: 60,
      }),
    } as Response);
    const client = new GatewayClient({
      apiKey: "server-key",
      baseURL: "https://gateway.test",
    });

    await client.auth.generateEphemeralKey("webadmin-key", {
      metadata: { purpose: "test" },
    });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("https://gateway.test/v1/auth/ephemeral-key");
    expect(init).toEqual(expect.objectContaining({ method: "POST" }));
    expect(init?.headers).toEqual(
      expect.objectContaining({ Authorization: "Bearer webadmin-key" }),
    );
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
