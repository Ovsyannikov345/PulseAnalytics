import type { TimeBucket } from '../graphql/generated/enums';
import { formatBucketStart, formatDayMonth, formatHourMinute } from './formatting';
import { bucketGrid } from './timeBuckets';
import type { TimeRange } from './timeRanges';

/** Steps on the local clock, so labels stay on the same hours across a DST change. */
export function localHourTicks(range: TimeRange, stepHours: number): Date[] {
  const tick = new Date(range.from);
  if (tick.getMinutes() !== 0 || tick.getSeconds() !== 0 || tick.getMilliseconds() !== 0) {
    tick.setHours(tick.getHours() + 1, 0, 0, 0);
  }

  const ticks: Date[] = [];
  while (tick < range.to) {
    ticks.push(new Date(tick));
    tick.setHours(tick.getHours() + stepHours);
  }

  return ticks;
}

/** Steps on the local clock, from the finest; the first that gives few enough ticks is used. */
const minuteSteps = [15, 30, 60, 120, 180, 240, 360, 720];

const daySteps = [1, 2, 3, 7, 14];

const LONGEST_DAY_STEP = 30;

/** Enough labels to read the axis, few enough to fit a phone. */
export const MAX_AXIS_TICKS = 8;

export interface AxisTick {
  time: number;
  label: string;
}

/** From the first full step after `from` (a full hour for hour steps), on the local clock. */
function localMinuteTicks(range: TimeRange, stepMinutes: number): Date[] {
  if (stepMinutes >= 60) {
    return localHourTicks(range, stepMinutes / 60);
  }

  const tick = new Date(range.from);
  tick.setSeconds(0, 0);
  if (tick < range.from) {
    tick.setMinutes(tick.getMinutes() + 1);
  }
  tick.setMinutes(Math.ceil(tick.getMinutes() / stepMinutes) * stepMinutes);

  const ticks: Date[] = [];
  while (tick < range.to) {
    ticks.push(new Date(tick));
    tick.setMinutes(tick.getMinutes() + stepMinutes);
  }
  return ticks;
}

/** Local midnights from the first one at or after `from`. */
function localMidnightTicks(range: TimeRange, stepDays: number): Date[] {
  const tick = new Date(range.from);
  tick.setHours(0, 0, 0, 0);
  if (tick < range.from) {
    tick.setDate(tick.getDate() + 1);
  }

  const ticks: Date[] = [];
  while (tick < range.to) {
    ticks.push(new Date(tick));
    tick.setDate(tick.getDate() + stepDays);
  }
  return ticks;
}

/** Every item, every second, third…: the smallest step from `daySteps` that keeps at most `maxCount`. */
function everyNth<T>(items: readonly T[], maxCount: number): T[] {
  const step =
    daySteps.find((candidate) => Math.ceil(items.length / candidate) <= maxCount) ??
    LONGEST_DAY_STEP;
  return items.filter((_, index) => index % step === 0);
}

/**
 * Ticks for a time axis over any range, at most `maxTicks`:
 * - local clock times every 15 min to 12 h, plus `to` at the end ("15:00 … 11:00, 14:30");
 * - else local midnights every 1, 2, 3, 7 or 14 days ("10 Nov");
 * - for DAY buckets the bucket starts, which are UTC midnights, labelled with their UTC date.
 */
export function timeAxis(
  range: TimeRange,
  bucket: TimeBucket,
  maxTicks: number = MAX_AXIS_TICKS,
): AxisTick[] {
  if (bucket === 'DAY') {
    return everyNth(bucketGrid(range, bucket), maxTicks).map((start) => ({
      time: start.getTime(),
      label: formatBucketStart(start, bucket),
    }));
  }

  for (const stepMinutes of minuteSteps) {
    const ticks = localMinuteTicks(range, stepMinutes);
    if (ticks.length < maxTicks) {
      return [...ticks, range.to].map((tick) => ({
        time: tick.getTime(),
        label: formatHourMinute(tick),
      }));
    }
  }

  const stepDays =
    daySteps.find((step) => localMidnightTicks(range, step).length <= maxTicks) ?? LONGEST_DAY_STEP;
  return localMidnightTicks(range, stepDays).map((tick) => ({
    time: tick.getTime(),
    label: formatDayMonth(tick),
  }));
}
