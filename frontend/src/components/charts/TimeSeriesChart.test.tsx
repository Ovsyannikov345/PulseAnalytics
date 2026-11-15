import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { type ChartBand, type ChartLine, TimeSeriesChart } from './TimeSeriesChart';

const bucketLengthMs = 10;

interface Point {
  time: number;
  a: number | null;
  b: number | null;
}

const lineA: ChartLine<Point> = { key: 'a', color: 'red', value: (point) => point.a };
const lineB: ChartLine<Point> = { key: 'b', color: 'blue', value: (point) => point.b };

function renderChart(
  a: (number | null)[],
  { b = a.map(() => null), band }: { b?: (number | null)[]; band?: ChartBand<Point> } = {},
) {
  const points = a.map((value, index) => ({
    time: index * bucketLengthMs,
    a: value,
    b: b[index] ?? null,
  }));
  return render(
    <TimeSeriesChart
      points={points}
      lines={band ? [lineA] : [lineA, lineB]}
      band={band}
      bucketLengthMs={bucketLengthMs}
      domain={[0, a.length * bucketLengthMs]}
      ticks={[0, 20]}
      formatTick={(time) => `t${String(time)}`}
      formatValue={(value) => `v${String(value)}`}
      renderTooltip={(point) => (point.a === null ? 'No samples' : `Value ${String(point.a)}`)}
      label="Average energy over the last 24 hours"
    />,
  );
}

describe('TimeSeriesChart', () => {
  it('is one image named by its label', () => {
    renderChart([1, 2, 3]);

    expect(
      screen.getByRole('img', { name: 'Average energy over the last 24 hours' }),
    ).toBeInTheDocument();
  });

  it('draws a line per series in its colour and the formatted ticks', () => {
    const { container } = renderChart([1, 2, 3], { b: [3, 2, 1] });

    const curves = container.querySelectorAll('.recharts-line-curve');
    expect(curves).toHaveLength(2);
    expect(curves[0]).toHaveAttribute('stroke', 'red');
    expect(curves[1]).toHaveAttribute('stroke', 'blue');
    expect(curves[0]).toHaveAttribute('d', expect.stringMatching(/^M/) as string);
    expect(screen.getByText('t0')).toBeInTheDocument();
    expect(screen.getByText('t20')).toBeInTheDocument();
  });

  it('draws a band between the two values of each point', () => {
    const { container } = renderChart([2, 3, 4], {
      band: {
        color: 'lightblue',
        range: (point) => (point.a === null ? null : [point.a - 1, point.a + 1]),
      },
    });

    const area = container.querySelector('.recharts-area-area');
    expect(area).toHaveAttribute('fill', 'lightblue');
    expect(area).toHaveAttribute('d', expect.stringMatching(/^M/) as string);
  });

  it('shades each run of buckets where no line has samples', () => {
    const { container } = renderChart([1, null, null, 2, null, 3]);

    expect(container.querySelectorAll('.recharts-reference-area')).toHaveLength(2);
  });

  it('shades no bucket where another line has samples', () => {
    const { container } = renderChart([1, null, null, 2], { b: [1, 2, null, 2] });

    expect(container.querySelectorAll('.recharts-reference-area')).toHaveLength(1);
  });

  it('shades nothing without gaps', () => {
    const { container } = renderChart([1, 2, 3]);

    expect(container.querySelectorAll('.recharts-reference-area')).toHaveLength(0);
  });

  it("shows the hovered bucket's tooltip", async () => {
    const { container } = renderChart([1, 2, 3]);
    const surface = container.querySelector('.recharts-wrapper');
    if (!surface) {
      throw new Error('The chart did not render.');
    }

    fireEvent.mouseMove(surface, { clientX: 400, clientY: 100 });

    expect(await screen.findByText(/^Value \d$/)).toBeInTheDocument();
  });
});
