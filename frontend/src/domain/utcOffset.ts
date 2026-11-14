/** The local offset from UTC in minutes, positive east of Greenwich (UTC+03:00 → 180). */
export function getUtcOffsetMinutes(date: Date = new Date()): number {
  // getTimezoneOffset() counts the other way round (UTC+03:00 → -180); `|| 0` avoids -0.
  return -date.getTimezoneOffset() || 0;
}

/** Formats an offset as "+03:00", "-05:00", "+05:30" or "+00:00". */
export function formatOffset(offsetMinutes: number): string {
  const sign = offsetMinutes < 0 ? '-' : '+';
  const absolute = Math.abs(offsetMinutes);
  const hours = String(Math.floor(absolute / 60)).padStart(2, '0');
  const minutes = String(absolute % 60).padStart(2, '0');
  return `${sign}${hours}:${minutes}`;
}

/** Formats an offset as "UTC+03:00", "UTC-05:00", "UTC+05:30" or "UTC+00:00". */
export function formatUtcOffset(offsetMinutes: number): string {
  return `UTC${formatOffset(offsetMinutes)}`;
}

/**
 * The instant as ISO 8601 in local time with its offset, "2026-11-14T15:00:00.000+03:00", as the app
 * sends times to the gateway. The offset is the date's own, so it is right on both sides of a DST change.
 */
export function toIsoWithOffset(date: Date): string {
  const offsetMinutes = getUtcOffsetMinutes(date);
  const local = new Date(date.getTime() + offsetMinutes * 60_000);
  return `${local.toISOString().slice(0, -1)}${formatOffset(offsetMinutes)}`;
}
