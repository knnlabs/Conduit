import {
  CHARS_PER_TOKEN_ESTIMATE,
  TOKENS_PER_IMAGE_ESTIMATE,
  TOKENS_PER_MESSAGE_ESTIMATE,
  TokenEstimator,
  TokenUtils,
  type EstimatorMessage,
} from "../token-estimator";

describe("TokenEstimator", () => {
  it("estimates message tokens from the character heuristic", () => {
    expect(TokenEstimator.estimateMessageTokens("")).toBe(0);
    expect(TokenEstimator.estimateMessageTokens("a".repeat(40))).toBe(
      40 / CHARS_PER_TOKEN_ESTIMATE,
    );
    // Partial tokens round up.
    expect(TokenEstimator.estimateMessageTokens("abcde")).toBe(2);
  });

  it("adds per-message and per-image scaffolding", () => {
    const messages: EstimatorMessage[] = [
      { role: "user", content: "a".repeat(8) },
      {
        role: "assistant",
        content: "b".repeat(4),
        images: [{ detail: "auto" }, { detail: "high" }],
      },
    ];

    const expected =
      2 +
      TOKENS_PER_MESSAGE_ESTIMATE +
      (1 + TOKENS_PER_MESSAGE_ESTIMATE + 2 * TOKENS_PER_IMAGE_ESTIMATE);

    expect(TokenEstimator.estimateConversationTokens(messages)).toEqual({
      prompt: expected,
      completion: 0,
      total: expected,
    });
  });

  it("flags context pressure thresholds", () => {
    const analysis = TokenEstimator.analyzeTokenUsage(
      { prompt: 95, completion: 0, total: 95 },
      100,
    );

    expect(analysis.percentage).toBe(95);
    expect(analysis.remaining).toBe(5);
    expect(analysis.isApproachingLimit).toBe(true);
    expect(analysis.isWarning).toBe(true);
    expect(analysis.isNearLimit).toBe(true);
    expect(analysis.isCritical).toBe(true);
  });

  it("clamps percentage and remaining once the limit is exceeded", () => {
    const analysis = TokenEstimator.analyzeTokenUsage(
      { prompt: 250, completion: 0, total: 250 },
      100,
    );

    expect(analysis.percentage).toBe(100);
    expect(analysis.remaining).toBe(0);
  });
});

describe("TokenUtils", () => {
  it("formats token counts with units", () => {
    expect(TokenUtils.formatTokenCount(999)).toBe("999");
    expect(TokenUtils.formatTokenCount(1500)).toBe("1.5K");
    expect(TokenUtils.formatTokenCount(2_500_000)).toBe("2.5M");
  });

  it("maps usage percentage to a colour band", () => {
    expect(TokenUtils.getUsageColor(10)).toBe("green");
    expect(TokenUtils.getUsageColor(60)).toBe("orange");
    expect(TokenUtils.getUsageColor(80)).toBe("yellow");
    expect(TokenUtils.getUsageColor(95)).toBe("red");
  });
});
