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
