/** A point of a time series: the bucket's start (epoch ms) and its value, `null` where it had no samples. */
export interface TimeSeriesPoint {
  time: number;
  value: number | null;
}

/** A span without samples, from the start of its first empty bucket to the end of its last. */
export interface GapRun {
  from: number;
  to: number;
}

/** The runs of consecutive empty buckets, which a chart shades as "No samples". */
export function gapRuns(points: readonly TimeSeriesPoint[], bucketLengthMs: number): GapRun[] {
  const runs: GapRun[] = [];
  let current: GapRun | null = null;

  for (const point of points) {
    if (point.value !== null) {
      current = null;
    } else if (current) {
      current.to = point.time + bucketLengthMs;
    } else {
      current = { from: point.time, to: point.time + bucketLengthMs };
      runs.push(current);
    }
  }

  return runs;
}
