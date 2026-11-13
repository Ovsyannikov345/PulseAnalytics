/**
 * Joins class names, skipping falsy ones: `cx(styles.link, isActive && styles.active)`.
 * CSS module lookups are typed `string | undefined` (noUncheckedIndexedAccess), so they are accepted as is.
 */
export function cx(...classNames: (string | false | null | undefined)[]): string {
  return classNames.filter(Boolean).join(' ');
}
