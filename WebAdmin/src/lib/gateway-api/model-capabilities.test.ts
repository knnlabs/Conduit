import { ModelCapability } from "./types";

describe("ModelCapability", () => {
  it("uses the Gateway discovery filter vocabulary", () => {
    expect(Object.values(ModelCapability)).toEqual([
      "chat",
      "chat_stream",
      "embeddings",
      "image_input",
      "video_input",
      "audio_input",
      "file_input",
      "pdf_input",
      "image_generation",
      "vision",
      "video_generation",
      "video_understanding",
      "function_calling",
      "speech_to_text",
      "text_to_speech",
      "rerank",
      "tool_use",
      "json_mode",
    ]);
  });

  it("keeps every capability in snake_case", () => {
    for (const capability of Object.values(ModelCapability)) {
      expect(capability).toMatch(/^[a-z]+(?:_[a-z]+)*$/);
    }
  });
});
