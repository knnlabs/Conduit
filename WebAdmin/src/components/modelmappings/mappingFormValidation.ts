import type { ModelProviderMappingDto } from "@/lib/admin-api";

interface MappingValidationValues {
  providerId?: string | number | null;
  associationProviderId?: string | null;
}

function selectedProviderId(values: MappingValidationValues): number | null {
  if (values.associationProviderId) {
    return Number(values.associationProviderId.split(":")[1]);
  }
  if (
    values.providerId !== undefined &&
    values.providerId !== null &&
    values.providerId !== ""
  ) {
    return Number(values.providerId);
  }
  return null;
}

export function createMappingFormValidation(
  mappings: ModelProviderMappingDto[],
  excludedId?: number,
) {
  return {
    modelAlias: (
      value: string,
      values: MappingValidationValues,
    ): string | null => {
      const alias = value.trim();
      if (!alias) return "Model alias is required";

      const providerId = selectedProviderId(values);
      const duplicate =
        providerId === null
          ? undefined
          : mappings.find(
              (mapping) =>
                mapping.id !== excludedId &&
                mapping.providerId === providerId &&
                mapping.modelAlias.toLowerCase() === alias.toLowerCase(),
            );

      return duplicate
        ? `Model alias '${alias}' already exists for this provider`
        : null;
    },
    priority: (value: number): string | null =>
      value < 0 || value > 1000
        ? "Priority must be between 0 and 1000"
        : null,
  };
}
