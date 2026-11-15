import type { ReactNode } from 'react';
import {
  Area,
  CartesianGrid,
  ComposedChart,
  Line,
  ReferenceArea,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { gapRuns } from './gapRuns';
import styles from './TimeSeriesChart.module.css';

const axisTick = { fill: 'var(--color-text-muted)', fontSize: 11 };

/** One bucket on the time axis: `time` is the bucket's start in epoch milliseconds. */
export interface TimePoint {
  time: number;
}

/** A line through the points; `null` breaks it (a bucket without samples). */
export interface ChartLine<TPoint extends TimePoint> {
  key: string;
  color: string;
  value: (point: TPoint) => number | null;
}

/** A filled band between two values per point, e.g. minimum to maximum. */
export interface ChartBand<TPoint extends TimePoint> {
  color: string;
  range: (point: TPoint) => [number, number] | null;
}

interface TimeSeriesChartProps<TPoint extends TimePoint> {
  points: readonly TPoint[];
  lines: readonly ChartLine<TPoint>[];
  band?: ChartBand<TPoint>;
  bucketLengthMs: number;
  domain: [number, number];
  ticks: readonly number[];
  formatTick: (time: number) => string;
  formatValue: (value: number) => string;
  renderTooltip: (point: TPoint) => ReactNode;
  label: string;
  height?: number;
}

interface PointTooltipProps<TPoint extends TimePoint> {
  points: readonly TPoint[];
  renderTooltip: (point: TPoint) => ReactNode;
  active?: boolean;
  label?: unknown;
}

function PointTooltip<TPoint extends TimePoint>({
  points,
  renderTooltip,
  active,
  label,
}: Readonly<PointTooltipProps<TPoint>>) {
  const point = active ? points.find((candidate) => candidate.time === label) : undefined;
  return point ? <div className={styles.tooltip}>{renderTooltip(point)}</div> : null;
}

/**
 * Lines (and an optional band) over time buckets. Lines break where a bucket has no samples; buckets
 * where no line has samples are shaded as gaps.
 */
export function TimeSeriesChart<TPoint extends TimePoint>({
  points,
  lines,
  band,
  bucketLengthMs,
  domain,
  ticks,
  formatTick,
  formatValue,
  renderTooltip,
  label,
  height = 270,
}: Readonly<TimeSeriesChartProps<TPoint>>) {
  const presence = points.map((point) => ({
    time: point.time,
    value: lines.some((line) => line.value(point) !== null) ? 0 : null,
  }));

  return (
    <div role="img" aria-label={label} className={styles.chart}>
      <ResponsiveContainer width="100%" height={height}>
        <ComposedChart
          data={points}
          margin={{ top: 8, right: 12, bottom: 0, left: 0 }}
          accessibilityLayer={false}
        >
          <CartesianGrid vertical={false} stroke="var(--color-gridline)" />
          <XAxis
            dataKey="time"
            type="number"
            scale="time"
            domain={domain}
            allowDataOverflow
            ticks={ticks}
            tickFormatter={formatTick}
            interval="preserveStartEnd"
            tick={axisTick}
            tickLine={false}
            axisLine={{ stroke: 'var(--color-border-strong)' }}
          />
          <YAxis
            width={48}
            domain={[0, 'auto']}
            tickFormatter={formatValue}
            tick={axisTick}
            tickLine={false}
            axisLine={false}
          />
          {gapRuns(presence, bucketLengthMs).map((gap) => (
            <ReferenceArea
              key={gap.from}
              x1={gap.from}
              x2={gap.to}
              ifOverflow="hidden"
              fill="var(--color-surface-muted)"
              fillOpacity={1}
              stroke="var(--color-border-strong)"
              strokeDasharray="3 3"
            />
          ))}
          <Tooltip
            filterNull={false}
            isAnimationActive={false}
            cursor={{ stroke: 'var(--color-text-secondary)' }}
            content={<PointTooltip points={points} renderTooltip={renderTooltip} />}
          />
          {band ? (
            <Area
              dataKey={band.range}
              type="linear"
              connectNulls={false}
              fill={band.color}
              fillOpacity={1}
              stroke="none"
              activeDot={false}
              isAnimationActive={false}
            />
          ) : null}
          {lines.map((line) => (
            <Line
              key={line.key}
              name={line.key}
              dataKey={line.value}
              type="linear"
              connectNulls={false}
              dot={false}
              activeDot={{
                r: 5,
                fill: line.color,
                stroke: 'var(--color-surface)',
                strokeWidth: 2,
              }}
              stroke={line.color}
              strokeWidth={2}
              isAnimationActive={false}
            />
          ))}
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  );
}
