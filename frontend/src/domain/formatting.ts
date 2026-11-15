import type { TimeBucket } from '../graphql/generated/enums';
import { bucketLengthMs } from './timeBuckets';

const LOCALE = 'en-GB';

const MINUTES_PER_DAY = 24 * 60;

const HOUR_MS = 60 * 60 * 1000;

const DAY_MS = 24 * HOUR_MS;

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

export function formatElapsed(milliseconds: number): string {
  const totalMinutes = Math.max(0, Math.floor(milliseconds / 60_000));

  if (totalMinutes < 60) {
    return `${String(totalMinutes)} min`;
  }

  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return minutes === 0 ? `${String(hours)} h` : `${String(hours)} h ${String(minutes)} min`;
}

// `Intl.DateTimeFormat` is slow to create, and `toLocale…String` with options creates one per call. A
// cached formatter would keep the time zone it was created in, so local fields come from `Date`'s getters
// and dates go through one formatter fixed to UTC.
const utcDayMonth = new Intl.DateTimeFormat(LOCALE, {
  day: 'numeric',
  month: 'short',
  timeZone: 'UTC',
});

function twoDigits(value: number): string {
  return String(value).padStart(2, '0');
}

/** Local time with seconds: "14:29:47". */
export function formatTime(date: Date): string {
  return `${formatHourMinute(date)}:${twoDigits(date.getSeconds())}`;
}

/** Local time: "14:30". */
export function formatHourMinute(date: Date): string {
  return `${twoDigits(date.getHours())}:${twoDigits(date.getMinutes())}`;
}

/** Local date: "10 Nov". */
export function formatDayMonth(date: Date): string {
  return utcDayMonth.format(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
}

/** Local date and time: "10 Nov 14:30". */
export function formatDateTime(date: Date): string {
  return `${formatDayMonth(date)} ${formatHourMinute(date)}`;
}

/** Local date and time with seconds: "15 Nov 14:29:47". */
export function formatDateTimeWithSeconds(date: Date): string {
  return `${formatDayMonth(date)} ${formatTime(date)}`;
}

/**
 * A bucket's label on a chart axis: the local time, or for DAY buckets the date. A DAY bucket is one
 * UTC day, so its label is the UTC date; west of UTC the local date at its start would be the day before.
 */
export function formatBucketStart(start: Date, bucket: TimeBucket): string {
  return bucket === 'DAY' ? utcDayMonth.format(start) : formatHourMinute(start);
}

/** With the date: a 24 h chart holds the same hour twice. */
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

/** The longest range a bucket size allows, rounded down: whole hours under two days ("16 h"), else days ("3 d"). */
export function formatRangeLimit(milliseconds: number): string {
  const hours = Math.floor(milliseconds / HOUR_MS);
  return hours < 48 ? `${String(hours)} h` : `${String(Math.floor(milliseconds / DAY_MS))} d`;
}

/** A range's length in days, rounded up to a tenth, so a range just over a limit never reads as the limit. */
export function formatDays(milliseconds: number): string {
  const days = Math.ceil((milliseconds / DAY_MS) * 10) / 10;
  return `${formatNumber(days, Number.isInteger(days) ? 0 : 1)} ${days === 1 ? 'day' : 'days'}`;
}

/** The local date and time as a `datetime-local` input takes it: "2026-11-14T09:05". */
export function toDateTimeLocalValue(date: Date): string {
  const day = `${String(date.getFullYear())}-${twoDigits(date.getMonth() + 1)}-${twoDigits(date.getDate())}`;
  return `${day}T${twoDigits(date.getHours())}:${twoDigits(date.getMinutes())}`;
}
