import type { ModelProviderMappingDto } from "@/lib/admin-api";
import { createMappingFormValidation } from "@/components/modelmappings/mappingFormValidation";
import {
  MIN_MODEL_TOKEN_LIMIT,
  modelFormValidation,
} from "../modelFormValidation";

describe("shared model form validation", () => {
  it.each([null, undefined, MIN_MODEL_TOKEN_LIMIT, 4096])(
    "accepts an empty or valid token limit (%s)",
    (value) => {
      expect(modelFormValidation.maxInputTokens(value)).toBeNull();
      expect(modelFormValidation.maxOutputTokens(value)).toBeNull();
    },
  );

  it("rejects token limits below the backend minimum", () => {
    expect(modelFormValidation.maxInputTokens(500)).toBe(
      `Minimum value is ${MIN_MODEL_TOKEN_LIMIT} tokens`,
    );
  });
});

describe("shared model-mapping form validation", () => {
  const mappings = [
    { id: 7, modelAlias: "nova", providerId: 3 },
  ] as ModelProviderMappingDto[];

  it("checks aliases against the selected provider in both form shapes", () => {
    const rules = createMappingFormValidation(mappings);

    expect(
      rules.modelAlias("NOVA", { associationProviderId: "11:3" }),
    ).toBe("Model alias 'NOVA' already exists for this provider");
    expect(rules.modelAlias("nova", { providerId: "3" })).toBe(
      "Model alias 'nova' already exists for this provider",
    );
    expect(rules.modelAlias("nova", { providerId: "4" })).toBeNull();
  });

  it("excludes the edited mapping and enforces the priority range", () => {
    const rules = createMappingFormValidation(mappings, 7);

    expect(rules.modelAlias("nova", { providerId: "3" })).toBeNull();
    expect(rules.priority(-1)).toBe(
      "Priority must be between 0 and 1000",
    );
    expect(rules.priority(1001)).toBe(
      "Priority must be between 0 and 1000",
    );
    expect(rules.priority(1000)).toBeNull();
  });
});
