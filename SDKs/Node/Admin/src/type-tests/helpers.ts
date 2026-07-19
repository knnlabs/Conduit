/**
 * Compile-time helpers for asserting that manually written SDK model types stay
 * structurally compatible with the OpenAPI-generated wire types in
 * `src/generated/admin-api.ts`.
 *
 * These are pure type-level checks: the files in this directory are never
 * imported at runtime and produce no JavaScript output. They are validated by
 * `npm run type-check` (tsc --noEmit), which CI runs. When the backend changes
 * a DTO, the regenerated wire types make the corresponding assertion fail to
 * compile, pointing at the exact model that drifted.
 */

/** Fails to compile unless T is exactly `true`. */
export type Expect<T extends true> = T;

/** Keys present in A but missing from B (resolves to `never` when none). */
export type MissingKeys<A, B> = Exclude<keyof A, keyof B>;

/**
 * True when A and B declare exactly the same property names.
 *
 * This is the looser check: it catches renamed, added, and removed properties
 * (the most common drift) while ignoring per-property type differences. Prefer
 * `Compatible` when it holds; fall back to this when nullability/enum encoding
 * differences between hand-written and generated types create noise.
 */
export type SameKeys<A, B> = [MissingKeys<A, B>, MissingKeys<B, A>] extends [never, never]
  ? true
  : false;

/**
 * Recursively strips optionality and `null | undefined` from all properties so
 * that differing nullability styles (`field?: string` vs `field: string | null`)
 * don't obscure real structural drift.
 */
export type DeepRequired<T> = T extends (infer E)[]
  ? DeepRequired<E>[]
  : T extends object
    ? { [K in keyof T]-?: DeepRequired<NonNullable<T[K]>> }
    : T;

/**
 * True when A and B are mutually assignable after deep nullability
 * normalization — the strict check: same keys AND compatible property types.
 */
export type Compatible<A, B> = [DeepRequired<A>] extends [DeepRequired<B>]
  ? [DeepRequired<B>] extends [DeepRequired<A>]
    ? true
    : false
  : false;
