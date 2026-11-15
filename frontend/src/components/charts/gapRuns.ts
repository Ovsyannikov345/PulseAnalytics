export interface TimeSeriesPoint {
  time: number;
  value: number | null;
}

export interface GapRun {
  from: number;
  to: number;
}

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
