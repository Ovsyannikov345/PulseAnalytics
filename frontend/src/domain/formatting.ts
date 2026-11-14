import type { TimeBucket } from '../graphql/generated/enums';

// British English, as in the mockups: "3,012.52", "14:29:47" and "10 Nov".
// Dates are formatted per call, not with cached formatters, so they follow the current time zone.
const LOCALE = 'en-GB';

const MINUTES_PER_DAY = 24 * 60;

const numberFormats = new Map<number, Intl.NumberFormat>();

/** A number with thousands separators and a fixed number of decimals: "3,012.52". */
export function formatNumber(value: number, fractionDigits = 0): string {
  let format = numberFormats.get(fractionDigits);

  if (!format) {
    format = new Intl.NumberFormat(LOCALE, {
      minimumFractionDigits: fractionDigits,
      maximumFractionDigits: fractionDigits,
    });
    numberFormats.set(fractionDigits, format);
  }

  return format.format(value);
}

/** A ratio from 0 to 1 as a percentage with a space before the sign: 0.77 → "77 %". */
export function formatPercent(ratio: number, fractionDigits = 0): string {
  return `${formatNumber(ratio * 100, fractionDigits)} %`;
}

/** Local time with seconds: "14:29:47". */
export function formatTime(date: Date): string {
  return date.toLocaleTimeString(LOCALE, {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  });
}

/** Local time: "14:30". */
export function formatHourMinute(date: Date): string {
  return date.toLocaleTimeString(LOCALE, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
}

/** Local date: "10 Nov". */
export function formatDayMonth(date: Date): string {
  return date.toLocaleDateString(LOCALE, { day: 'numeric', month: 'short' });
}

/** Local date and time: "10 Nov 14:30". */
export function formatDateTime(date: Date): string {
  return `${formatDayMonth(date)} ${formatHourMinute(date)}`;
}

/**
 * A bucket's label on a chart axis: the local time, or for DAY buckets the date. A DAY bucket is one
 * UTC day, so its label is the UTC date; west of UTC the local date at its start would be the day before.
 */
export function formatBucketStart(start: Date, bucket: TimeBucket): string {
  return bucket === 'DAY'
    ? start.toLocaleDateString(LOCALE, { day: 'numeric', month: 'short', timeZone: 'UTC' })
    : formatHourMinute(start);
}

/**
 * The hint for DAY buckets when the local time isn't UTC: "Days start at 03:00 local time (00:00 UTC)".
 * `null` at UTC, where no hint is needed.
 */
export function formatDayStartHint(offsetMinutes: number): string | null {
  if (offsetMinutes === 0) {
    return null;
  }

  // UTC midnight on the local clock: the offset itself, wrapped into one day (-05:00 → 19:00).
  const minutesOfDay = ((offsetMinutes % MINUTES_PER_DAY) + MINUTES_PER_DAY) % MINUTES_PER_DAY;
  const hours = String(Math.floor(minutesOfDay / 60)).padStart(2, '0');
  const minutes = String(minutesOfDay % 60).padStart(2, '0');
  return `Days start at ${hours}:${minutes} local time (00:00 UTC)`;
}
