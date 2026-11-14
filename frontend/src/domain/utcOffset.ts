/** The local offset from UTC in minutes, positive east of Greenwich (UTC+03:00 → 180). */
export function getUtcOffsetMinutes(date: Date = new Date()): number {
  // getTimezoneOffset() counts the other way round (UTC+03:00 → -180); `|| 0` avoids -0.
  return -date.getTimezoneOffset() || 0;
}

/** Formats an offset as "UTC+03:00", "UTC-05:00", "UTC+05:30" or "UTC+00:00". */
export function formatUtcOffset(offsetMinutes: number): string {
  const sign = offsetMinutes < 0 ? '-' : '+';
  const absolute = Math.abs(offsetMinutes);
  const hours = String(Math.floor(absolute / 60)).padStart(2, '0');
  const minutes = String(absolute % 60).padStart(2, '0');
  return `UTC${sign}${hours}:${minutes}`;
}
