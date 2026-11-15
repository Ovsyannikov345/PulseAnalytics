import type { TimeRange } from './timeRanges';

/**
 * Ticks for a time axis on full local hours, `stepHours` apart: from the first full hour at or after
 * `from` to the last one before `to`. Steps are taken on the local clock, so the labels stay regular
 * (00:00, 04:00, 08:00) across a daylight saving change, where one step is an hour longer or shorter.
 */
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
