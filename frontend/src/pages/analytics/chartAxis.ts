import { timeAxis } from '../../domain/axisTicks';
import { formatDateTime } from '../../domain/formatting';
import { bucketStartOf } from '../../domain/timeBuckets';
import type { TimeRange } from '../../domain/timeRanges';
import type { TimeBucket } from '../../graphql/generated/enums';

/** The time axis of an Analytics chart: from the first bucket's start to the end of the range. */
export function chartAxis(range: TimeRange, bucket: TimeBucket) {
  const ticks = timeAxis(range, bucket);
  const labels = new Map(ticks.map((tick) => [tick.time, tick.label]));

  return {
    domain: [bucketStartOf(range.from, bucket).getTime(), range.to.getTime()] as [number, number],
    ticks: ticks.map((tick) => tick.time),
    formatTick: (time: number) => labels.get(time) ?? '',
  };
}

/** "from 14 Nov 14:30 to 15 Nov 14:30", for the charts' text alternatives. */
export function describeRange(range: TimeRange): string {
  return `from ${formatDateTime(range.from)} to ${formatDateTime(range.to)}`;
}
