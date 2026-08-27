// Joins class names, dropping anything falsy. Deliberately not a clsx dependency: the whole
// need here is "skip the undefined ones", and every component in src/ui composes its classes
// from plain conditionals.
export function cx(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(" ");
}

/**
 * Reads a value out of a static variant table.
 *
 * Every component in src/ui maps a prop like `variant="ghost"` or `size="sm"` onto a class string
 * through a frozen lookup object. `security/detect-object-injection` flags each of those as a
 * possible injection sink, which it is not: TypeScript constrains `key` to the table's own keys, so
 * the caller cannot pass anything the table does not already declare, and no user input reaches the
 * index. This helper exists so that reasoning is written down once and the rule is silenced in one
 * place, rather than twenty inline comments nobody reads.
 */
export function styleFor<K extends string, V>(table: Record<K, V>, key: K): V {
  // eslint-disable-next-line security/detect-object-injection -- key is a literal union of the table's own keys, see above
  return table[key];
}
