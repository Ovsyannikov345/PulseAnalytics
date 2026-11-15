import type { TimeBucket } from '../graphql/generated/enums';
import { bucketLengthMs } from './timeBuckets';

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

/** Whole minutes elapsed: "4 min", "1 h 5 min", "3 h"; partial minutes are dropped. */
export function formatElapsed(milliseconds: number): string {
  const totalMinutes = Math.max(0, Math.floor(milliseconds / 60_000));

  if (totalMinutes < 60) {
    return `${String(totalMinutes)} min`;
  }

  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return minutes === 0 ? `${String(hours)} h` : `${String(hours)} h ${String(minutes)} min`;
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
 * A bucket's time span for tooltips: "15 Nov 08:45–09:00", or for a DAY bucket its UTC date ("15 Nov").
 * The date is part of it because a chart of the last 24 hours holds the same hour twice.
 */
export function formatBucketRange(start: Date, bucket: TimeBucket): string {
  if (bucket === 'DAY') {
    return formatBucketStart(start, bucket);
  }

  const end = new Date(start.getTime() + bucketLengthMs(bucket));
  return `${formatDateTime(start)}–${formatHourMinute(end)}`;
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
